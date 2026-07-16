# Kubix UTN — orquestación local
# Uso: make help
#
# El .env raíz es la única fuente de verdad. No hacemos `include .env`
# (rompe Make con passwords/`!`). Los scripts cargan el env ellos mismos.

SHELL := /bin/bash
.DEFAULT_GOAL := help

ROOT := $(abspath $(dir $(lastword $(MAKEFILE_LIST))))
SCRIPTS := $(ROOT)/scripts

DEVICE ?=

.PHONY: help sync-env env doctor \
	db db-down tools \
	api web mobile mobile-chrome mobile-android mobile-ios \
	up down docker docker-dev docker-down \
	logs logs-api logs-web \
	test test-be test-web test-mobile coverage

help: ## Muestra targets disponibles
	@grep -hE '^[a-zA-Z0-9_-]+:.*?## .*$$' $(ROOT)/Makefile | \
		awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-18s\033[0m %s\n", $$1, $$2}'
	@echo ""
	@echo "Variables (.env raíz): API_URL, GOOGLE_MAPS_API_KEY, MOBILE_API_URL, MOBILE_DEVICE"
	@echo "Mobile: make mobile DEVICE=chrome | make mobile-android | DEVICE=<id> make mobile"

sync-env: ## Genera web/.env + Maps iOS/Flutter web desde .env raíz
	@$(SCRIPTS)/sync-env.sh

env: sync-env ## Alias de sync-env + imprime resumen
	@source $(SCRIPTS)/lib/env.sh && kubix_load_env && \
	  echo "API_URL=$$API_URL" && \
	  echo "GOOGLE_MAPS_API_KEY len=$${#GOOGLE_MAPS_API_KEY}" && \
	  echo "MOBILE_API_URL=$${MOBILE_API_URL:-"(auto)"}" && \
	  echo "MOBILE_DEVICE=$${MOBILE_DEVICE:-"(flutter default)"}"

doctor: ## Comprueba herramientas y .env
	@command -v docker >/dev/null || (echo "falta docker"; exit 1)
	@command -v dotnet >/dev/null || (echo "falta dotnet SDK"; exit 1)
	@command -v node >/dev/null || (echo "falta node"; exit 1)
	@command -v flutter >/dev/null || echo "aviso: falta flutter (solo mobile)"
	@test -f $(ROOT)/.env || (echo "falta .env — cp .env.example .env"; exit 1)
	@$(SCRIPTS)/sync-env.sh
	@echo "OK doctor"

# --- Infra ---

db: ## Solo Postgres (Docker)
	docker compose -f $(ROOT)/docker-compose.yml up -d postgres

db-down: ## Para Postgres (conserva volume)
	docker compose -f $(ROOT)/docker-compose.yml stop postgres

tools: ## pgAdmin (profile tools)
	docker compose -f $(ROOT)/docker-compose.yml --profile tools up -d

# --- Apps individuales ---

api: sync-env db ## API con hot reload (dotnet watch)
	@source $(SCRIPTS)/lib/env.sh && kubix_load_env && \
	  echo "→ API $$API_URL  env=$$ASPNETCORE_ENVIRONMENT  Maps key len=$${#GOOGLE_MAPS_API_KEY}" && \
	  cd $(ROOT)/backend && \
	  ASPNETCORE_ENVIRONMENT="$$ASPNETCORE_ENVIRONMENT" \
	  ConnectionStrings__Default="$$(kubix_connection_string)" \
	  GoogleMaps__ApiKey="$$GOOGLE_MAPS_API_KEY" \
	  Cors__WebOrigin="$$WEB_ORIGIN" \
	  Cors__AllowedOrigins__0="$$WEB_ORIGIN" \
	  Cors__AllowedOrigins__1="http://127.0.0.1:$$WEB_PORT" \
	  Cors__AllowedOrigins__2="$$FLUTTER_WEB_ORIGIN" \
	  Cors__AllowedOrigins__3="http://127.0.0.1:$$FLUTTER_WEB_PORT" \
	  Database__MigrateOnStartup="$$MIGRATE_ON_STARTUP" \
	  Database__SeedOnStartup="$$SEED_ON_STARTUP" \
	  ASPNETCORE_URLS="http://0.0.0.0:$$API_PORT" \
	  dotnet watch run --project src/Kubix.Api --no-launch-profile

web: sync-env ## Admin web (Vite :5173 strict)
	@source $(SCRIPTS)/lib/env.sh && kubix_load_env && \
	  if [[ ! -d $(ROOT)/web/node_modules ]]; then cd $(ROOT)/web && npm install; fi && \
	  cd $(ROOT)/web && npm run dev -- --port "$$WEB_PORT" --strictPort --host

mobile: sync-env ## Flutter (DEVICE=… o MOBILE_DEVICE en .env)
	@$(SCRIPTS)/mobile-run.sh $(DEVICE)

mobile-chrome: ## Flutter web en Chrome
	@$(SCRIPTS)/mobile-run.sh chrome

mobile-android: ## Emulador/dispositivo Android (DEVICE opcional)
	@$(SCRIPTS)/mobile-run.sh $(or $(DEVICE),emulator-5554)

mobile-ios: ## Simulador/dispositivo iOS (DEVICE opcional)
	@$(SCRIPTS)/mobile-run.sh $(DEVICE)

# --- Todo junto ---

up: ## Postgres + API + Web (Ctrl+C detiene API/Web)
	@$(SCRIPTS)/dev-up.sh

down: ## Detiene API/Web (pids) + stack Docker compose
	@if [[ -f $(ROOT)/.run/api.pid ]]; then kill $$(cat $(ROOT)/.run/api.pid) 2>/dev/null || true; rm -f $(ROOT)/.run/api.pid; fi
	@if [[ -f $(ROOT)/.run/web.pid ]]; then kill $$(cat $(ROOT)/.run/web.pid) 2>/dev/null || true; rm -f $(ROOT)/.run/web.pid; fi
	@if [[ -f $(ROOT)/.run/dev-up.pid ]]; then kill $$(cat $(ROOT)/.run/dev-up.pid) 2>/dev/null || true; rm -f $(ROOT)/.run/dev-up.pid; fi
	docker compose -f $(ROOT)/docker-compose.yml down
	@echo "Stack detenido (volume Postgres se conserva)."

docker: ## Todo en Docker (API Release, sin hot reload)
	@$(SCRIPTS)/sync-env.sh
	docker compose -f $(ROOT)/docker-compose.yml up -d --build

docker-dev: ## Docker API con hot reload (compose.dev)
	@$(SCRIPTS)/sync-env.sh
	docker compose -f $(ROOT)/docker-compose.yml -f $(ROOT)/docker-compose.dev.yml up --build

docker-down: ## docker compose down
	docker compose -f $(ROOT)/docker-compose.yml down

logs: ## Logs docker compose
	docker compose -f $(ROOT)/docker-compose.yml logs -f --tail=100

logs-api: ## Últimas líneas log API local (.run)
	@tail -n 80 -f $(ROOT)/.run/api.log 2>/dev/null || echo "Sin .run/api.log — usa make up / make api en primer plano"

logs-web: ## Últimas líneas log Web local (.run)
	@tail -n 80 -f $(ROOT)/.run/web.log 2>/dev/null || echo "Sin .run/web.log — usa make up / make web en primer plano"

# --- QA / KBX-27 ---

test: test-be test-web test-mobile ## Corre suites + umbrales de cobertura (KBX-27)
	@echo "OK make test"

test-be: ## Backend xUnit + cobertura Application/Domain ≥70%
	@mkdir -p $(ROOT)/qa/results/backend-coverage $(ROOT)/backend/TestResults
	@rm -rf $(ROOT)/backend/TestResults/*
	cd $(ROOT)/backend && \
	  dotnet test tests/Kubix.Tests/Kubix.Tests.csproj \
	    --settings coverage.runsettings \
	    --results-directory $(ROOT)/backend/TestResults \
	    --collect:"XPlat Code Coverage"
	@COV=$$(find $(ROOT)/backend/TestResults -name 'coverage.cobertura.xml' | head -1) && \
	  test -n "$$COV" && \
	  cp "$$COV" $(ROOT)/qa/results/backend-coverage/coverage.cobertura.xml && \
	  $(ROOT)/scripts/check-cobertura-threshold.sh "$$COV" 70 'Kubix\.(Application|Domain)'

test-web: ## Web Vitest + cobertura pages/auth/lib ≥70%
	@mkdir -p $(ROOT)/qa/results/web-coverage
	@if [[ ! -d $(ROOT)/web/node_modules ]]; then cd $(ROOT)/web && npm install; fi
	cd $(ROOT)/web && npm run test:coverage

test-mobile: ## Mobile flutter_test + cobertura filtrada ≥60%
	@mkdir -p $(ROOT)/qa/results/mobile-coverage
	cd $(ROOT)/mobile && flutter test --coverage
	@$(ROOT)/scripts/check-mobile-coverage.sh \
	  $(ROOT)/mobile/coverage/lcov.info 60 \
	  $(ROOT)/mobile/coverage/lcov.filtered.info
	@cp $(ROOT)/mobile/coverage/lcov.filtered.info $(ROOT)/qa/results/mobile-coverage/lcov.filtered.info
	@cp $(ROOT)/mobile/coverage/lcov.info $(ROOT)/qa/results/mobile-coverage/lcov.info

coverage: test ## Alias de test (publica artefactos en qa/results/)
