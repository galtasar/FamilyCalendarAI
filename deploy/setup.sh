#!/bin/bash
# FamilyCalendarAI — Oracle Cloud VM setup script
# Run once on a fresh Ubuntu 22.04 ARM VM
# Usage: curl -fsSL https://raw.githubusercontent.com/galtasar/FamilyCalendarAI/main/deploy/setup.sh | bash

set -e

echo "=== FamilyCalendarAI Setup ==="

# 1. System update
echo "[1/6] Updating system packages..."
sudo apt-get update -qq
sudo apt-get upgrade -y -qq

# 2. Install Docker
echo "[2/6] Installing Docker..."
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker "$USER"

# 3. Open port 80 in OS firewall (Oracle Cloud also requires opening port 80 in the
#    Security List via the web console — see instructions below)
echo "[3/6] Opening port 80 in firewall..."
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 80 -j ACCEPT
sudo netfilter-persistent save 2>/dev/null || sudo apt-get install -y iptables-persistent

# 4. Clone repository
echo "[4/6] Cloning repository..."
if [ -d "FamilyCalendarAI" ]; then
  cd FamilyCalendarAI && git pull
else
  git clone https://github.com/galtasar/FamilyCalendarAI.git
  cd FamilyCalendarAI
fi

# 5. Set up environment file
echo "[5/6] Setting up environment..."
if [ ! -f ".env" ]; then
  cp .env.example .env
  echo ""
  echo "======================================================"
  echo "  ACTION REQUIRED: Edit the .env file with your keys"
  echo "  Run: nano .env"
  echo "======================================================"
  echo ""
  echo "Required values:"
  echo "  OPENAI_API_KEY      — your OpenAI API key"
  echo "  GMAIL_CLIENT_ID     — Google OAuth client ID"
  echo "  GMAIL_CLIENT_SECRET — Google OAuth client secret"
  echo "  GMAIL_REFRESH_TOKEN — run tools/GetRefreshToken locally to get this"
  echo "  APP_EMAIL           — email address for notifications"
  echo "  APP_BASE_URL        — http://$(curl -s ifconfig.me)"
  echo "  ADMIN_KEY           — any strong random string"
  echo "  POSTGRES_PASSWORD   — any strong password"
  echo ""
  echo "After editing .env, run:  cd FamilyCalendarAI && ./deploy/start.sh"
  exit 0
fi

# 6. Start application
echo "[6/6] Starting application..."
sudo docker compose up -d --build

echo ""
echo "======================================================"
echo "  Setup complete!"
PUBLIC_IP=$(curl -s ifconfig.me)
echo "  App is running at: http://$PUBLIC_IP"
echo ""
echo "  IMPORTANT: In the Oracle Cloud Console, ensure"
echo "  port 80 is open in your VCN Security List:"
echo "  Networking > Virtual Cloud Networks > your VCN"
echo "  > Security Lists > Ingress Rules > Add: TCP port 80"
echo "======================================================"
