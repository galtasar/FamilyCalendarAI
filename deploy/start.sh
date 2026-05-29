#!/bin/bash
# Start or restart FamilyCalendarAI
set -e

cd "$(dirname "$0")/.."

if [ ! -f ".env" ]; then
  echo "ERROR: .env file not found. Copy .env.example to .env and fill in your values."
  exit 1
fi

echo "Pulling latest changes..."
git pull

echo "Building and starting containers..."
sudo docker compose up -d --build

echo ""
echo "Status:"
sudo docker compose ps

PUBLIC_IP=$(curl -s ifconfig.me 2>/dev/null || echo "unknown")
echo ""
echo "App running at: http://$PUBLIC_IP"
