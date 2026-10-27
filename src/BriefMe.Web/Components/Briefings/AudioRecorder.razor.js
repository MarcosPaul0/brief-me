// Microphone capture for AudioRecorder.razor (REC-02..REC-11). Captures raw PCM through an AudioWorklet, downsamples
// it to 16 kHz mono as it arrives and encodes a WAV on stop — WAV is already an accepted attachment format, so the
// recording needs no server change (context #1). The take stays in the browser (object URL for the review player)
// until .NET asks for the bytes on "Adicionar aos anexos".

const TargetSampleRate = 16000;
const ProcessorName = "briefme-recorder";

// Batches the 128-frame render quanta so the main thread gets ~10 messages per second, not ~375.
const processorSource = `
class RecorderProcessor extends AudioWorkletProcessor {
    constructor() {
        super();
        this.buffer = new Float32Array(4096);
        this.length = 0;
    }

    process(inputs) {
        const channel = inputs[0] && inputs[0][0];
        if (channel) {
            if (this.length + channel.length > this.buffer.length) {
                this.port.postMessage(this.buffer.slice(0, this.length));
                this.length = 0;
            }
            this.buffer.set(channel, this.length);
            this.length += channel.length;
        }
        return true;
    }
}
registerProcessor("${ProcessorName}", RecorderProcessor);
`;

export function isSupported() {
    return window.isSecureContext
        && !!navigator.mediaDevices?.getUserMedia
        && typeof AudioContext !== "undefined"
        && typeof AudioWorkletNode !== "undefined";
}

export function createRecorder(dotNet, maxSeconds) {
    return new Recorder(dotNet, maxSeconds);
}

class Recorder {
    constructor(dotNet, maxSeconds) {
        this.dotNet = dotNet;
        this.maxSeconds = maxSeconds;
        this.capture = null;
        this.take = null;
    }

    /** Asks for the microphone and starts capturing; resolves to "ok" or the reason it could not. */
    async start() {
        this.discard();

        let stream;
        try {
            stream = await navigator.mediaDevices.getUserMedia({
                audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true }
            });
        } catch (error) {
            return startFailure(error);
        }

        try {
            // The context runs at the device rate: Firefox refuses to connect a stream to a context of another rate.
            const context = new AudioContext();
            const moduleUrl = URL.createObjectURL(new Blob([processorSource], { type: "application/javascript" }));
            try {
                await context.audioWorklet.addModule(moduleUrl);
            } finally {
                URL.revokeObjectURL(moduleUrl);
            }

            const source = context.createMediaStreamSource(stream);
            // No outputs: the node is a sink, pulled by the graph without being routed to the speakers.
            const node = new AudioWorkletNode(context, ProcessorName, { numberOfInputs: 1, numberOfOutputs: 0 });
            const capture = {
                stream,
                context,
                source,
                node,
                downsampler: new Downsampler(context.sampleRate, Math.min(TargetSampleRate, context.sampleRate)),
                chunks: [],
                samples: 0,
                lastTick: 0,
                limitReached: false,
                startedAt: new Date()
            };

            node.port.onmessage = event => this.onSamples(capture, event.data);
            source.connect(node);
            this.capture = capture;
            return "ok";
        } catch {
            stream.getTracks().forEach(track => track.stop());
            return "failed";
        }
    }

    onSamples(capture, input) {
        if (capture !== this.capture || capture.limitReached)
            return;

        const pcm = capture.downsampler.push(input);
        capture.chunks.push(pcm);
        capture.samples += pcm.length;

        const seconds = Math.floor(capture.samples / capture.downsampler.targetRate);
        if (seconds >= this.maxSeconds) {
            capture.limitReached = true;
            this.dotNet.invokeMethodAsync("OnRecordingLimitReached");
        } else if (seconds !== capture.lastTick) {
            capture.lastTick = seconds;
            this.dotNet.invokeMethodAsync("OnRecordingTick", seconds);
        }
    }

    /** Stops capturing, releases the microphone and keeps the take for review. */
    async stop() {
        const capture = this.capture;
        if (!capture)
            return null;

        this.capture = null;
        await release(capture);

        const rate = capture.downsampler.targetRate;
        const samples = Math.min(capture.samples, this.maxSeconds * rate);
        const blob = encodeWav(capture.chunks, samples, rate);
        const url = URL.createObjectURL(blob);
        this.take = { blob, url };

        return {
            url,
            sizeBytes: blob.size,
            samples,
            durationSeconds: Math.round(samples / rate),
            startedAt: formatStartedAt(capture.startedAt)
        };
    }

    /** Stops capturing and throws the take away. */
    async cancel() {
        const capture = this.capture;
        this.capture = null;
        if (capture)
            await release(capture);
        this.discard();
    }

    /** The WAV bytes of the reviewed take, read by .NET as a stream. */
    async takeBytes() {
        if (!this.take)
            throw new Error("No take to read.");
        return new Uint8Array(await this.take.blob.arrayBuffer());
    }

    discard() {
        if (this.take)
            URL.revokeObjectURL(this.take.url);
        this.take = null;
    }

    async dispose() {
        await this.cancel();
        this.dotNet = null;
    }
}

function startFailure(error) {
    switch (error?.name) {
        case "NotAllowedError":
        case "SecurityError":
            return "denied";
        case "NotFoundError":
        case "OverconstrainedError":
            return "no-device";
        case "NotReadableError":
        case "AbortError":
            return "busy";
        default:
            return "failed";
    }
}

async function release(capture) {
    capture.node.port.onmessage = null;
    capture.source.disconnect();
    capture.stream.getTracks().forEach(track => track.stop());
    await capture.context.close();
}

// Box-filter decimation from the device rate to the target rate. Keeps its phase across chunks so the joins
// between worklet batches add no clicks.
class Downsampler {
    constructor(sourceRate, targetRate) {
        this.targetRate = targetRate;
        this.ratio = sourceRate / targetRate;
        this.position = 0;
        this.sum = 0;
        this.count = 0;
    }

    push(input) {
        const output = new Int16Array(Math.ceil((input.length + this.position) / this.ratio) + 1);
        let length = 0;

        for (let i = 0; i < input.length; i++) {
            this.sum += input[i];
            this.count++;
            this.position++;

            if (this.position >= this.ratio) {
                const sample = Math.max(-1, Math.min(1, this.sum / this.count));
                output[length++] = sample < 0 ? sample * 0x8000 : sample * 0x7fff;
                this.sum = 0;
                this.count = 0;
                this.position -= this.ratio;
            }
        }

        return output.subarray(0, length);
    }
}

// PCM 16-bit little-endian, mono: the 44-byte RIFF/WAVE header followed by the samples.
function encodeWav(chunks, samples, sampleRate) {
    const dataBytes = samples * 2;
    const header = new DataView(new ArrayBuffer(44));
    const ascii = (offset, text) => [...text].forEach((c, i) => header.setUint8(offset + i, c.charCodeAt(0)));

    ascii(0, "RIFF");
    header.setUint32(4, 36 + dataBytes, true);
    ascii(8, "WAVE");
    ascii(12, "fmt ");
    header.setUint32(16, 16, true);
    header.setUint16(20, 1, true);
    header.setUint16(22, 1, true);
    header.setUint32(24, sampleRate, true);
    header.setUint32(28, sampleRate * 2, true);
    header.setUint16(32, 2, true);
    header.setUint16(34, 16, true);
    ascii(36, "data");
    header.setUint32(40, dataBytes, true);

    const parts = [header];
    let remaining = samples;
    for (const chunk of chunks) {
        if (remaining <= 0)
            break;
        const part = chunk.length <= remaining ? chunk : chunk.subarray(0, remaining);
        parts.push(part);
        remaining -= part.length;
    }

    return new Blob(parts, { type: "audio/wav" });
}

// Browser-local "yyyy-MM-dd-HHmmss" — the user's clock, not the server's, names the file (REC-04).
function formatStartedAt(date) {
    const pad = value => String(value).padStart(2, "0");
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
        + `-${pad(date.getHours())}${pad(date.getMinutes())}${pad(date.getSeconds())}`;
}
