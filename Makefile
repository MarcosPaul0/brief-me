.DEFAULT_GOAL := help

.PHONY: help watch css build run test up down db-up db-down db-logs minio-up minio-logs

WEB := src/BriefMe.Web

help: ## Mostra comandos disponiveis
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-15s\033[0m %s\n", $$1, $$2}'

css: ## Tailwind em modo watch (regenera app.css ao salvar)
	cd $(WEB) && ./tailwindcss -i ./Styles/app.tailwind.css -o ./wwwroot/app.css --watch

watch: ## Inicia app + tailwind em watch (hot reload)
	cd $(WEB) && ./tailwindcss -i ./Styles/app.tailwind.css -o ./wwwroot/app.css --watch & \
	dotnet watch run --project $(WEB) --launch-profile https; \
	kill %1 2>/dev/null

build: ## Builda solution
	dotnet build

run: ## Roda app sem watch
	dotnet run --project $(WEB) --launch-profile https

test: ## Roda testes
	dotnet test

up: ## Sobe toda a stack (Postgres + MinIO) em background
	docker compose up -d

down: ## Para a stack (mantem volumes/dados)
	docker compose down

db-up: ## Sobe Postgres em background
	docker compose up -d db

db-down: ## Para Postgres (mantem volume/dados)
	docker compose down

db-logs: ## Segue logs do Postgres
	docker compose logs -f db

minio-up: ## Sobe MinIO (S3 local) em background — console em http://localhost:9001
	docker compose up -d minio

minio-logs: ## Segue logs do MinIO
	docker compose logs -f minio
