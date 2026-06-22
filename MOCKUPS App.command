#!/bin/zsh

set -e

SCRIPT_DIR="${0:A:h}"
cd "$SCRIPT_DIR"

echo "MOCKUPS — App"
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
echo "Abriendo MOCKUPS en Electron..."
echo "Pulsa Ctrl+C para detener la app."
echo

npm run electron
