#!/bin/zsh

set -e

SCRIPT_DIR="${0:A:h}"
cd "$SCRIPT_DIR"

echo "MOCKUPS — Core app shell"
echo "Directorio: $SCRIPT_DIR"
echo

if [[ ! -d node_modules ]]; then
  echo "Instalando dependencias..."
  npm install
fi

if [[ ! -f data/mockups-dev.sqlite ]]; then
  echo "Creando y preparando la base de desarrollo..."
  npm run db:init
  npm run db:seed
fi

echo
echo "Abriendo http://127.0.0.1:4173"
(sleep 2 && open "http://127.0.0.1:4173") &

echo "Pulsa Ctrl+C para detener MOCKUPS."
echo

npm run app
