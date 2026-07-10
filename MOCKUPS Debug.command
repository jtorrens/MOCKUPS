#!/bin/zsh

set -e

SCRIPT_DIR="${0:A:h}"
cd "$SCRIPT_DIR"

echo "MOCKUPS — Desktop Editor"
echo "Directorio: $SCRIPT_DIR"
echo

if [[ ! -d node_modules ]]; then
  echo "Instalando dependencias..."
  npm install
fi

echo
echo "Abriendo MOCKUPS Desktop Editor..."
echo "Pulsa Ctrl+C para detener MOCKUPS."
echo

exec "$SCRIPT_DIR/run-desktop-spike.sh"
