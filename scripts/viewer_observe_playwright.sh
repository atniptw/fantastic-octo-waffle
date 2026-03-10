#!/usr/bin/env bash
set -euo pipefail

URL="http://localhost:8000/"
REPORT="data/outputs/viewer_observability.json"
STRICT="0"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --url)
      URL="$2"
      shift 2
      ;;
    --report)
      REPORT="$2"
      shift 2
      ;;
    --strict)
      STRICT="1"
      shift 1
      ;;
    *)
      echo "unknown argument: $1" >&2
      exit 2
      ;;
  esac
done

mkdir -p "$(dirname "$REPORT")"

if command -v npx >/dev/null 2>&1 && npx --yes playwright --version >/dev/null 2>&1; then
  tmp_script="$(mktemp)"
  cat > "$tmp_script" <<'NODE'
const fs = require("node:fs");
const { chromium } = require("playwright");

const url = process.env.VIEWER_URL;
const reportPath = process.env.REPORT_PATH;

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  const report = {
    url,
    started_at: new Date().toISOString(),
    timeline: [],
    console: [],
    selected_card: null,
    model_status: null,
    warnings: [],
  };

  page.on("console", (msg) => {
    report.console.push({ type: msg.type(), text: msg.text(), location: msg.location() });
  });

  report.timeline.push({ step: "goto", status: "start" });
  await page.goto(url, { waitUntil: "networkidle" });
  report.timeline.push({ step: "goto", status: "ok" });

  await page.waitForSelector("#summary", { timeout: 10000 });
  const summary = await page.locator("#summary").innerText();
  report.timeline.push({ step: "summary", status: "ok", summary });

  const cards = page.locator("#gallery .card");
  const cardCount = await cards.count();
  report.timeline.push({ step: "count_cards", status: "ok", card_count: cardCount });

  if (cardCount === 0) {
    report.warnings.push("No cards rendered in gallery");
  } else {
    const card = cards.first();
    const cardTitle = await card.locator(".title").innerText().catch(() => "<unknown>");
    report.selected_card = cardTitle;
    await card.click();
    report.timeline.push({ step: "click_first_card", status: "ok", card: cardTitle });
    await page.waitForTimeout(800);
    const modelStatus = await page.locator("#model-status").innerText();
    report.model_status = modelStatus;
    report.timeline.push({ step: "model_status", status: "ok", model_status: modelStatus });
    if (/failed to load/i.test(modelStatus)) {
      report.warnings.push("Model status indicates load failure");
    }
  }

  report.finished_at = new Date().toISOString();
  await browser.close();
  fs.writeFileSync(reportPath, JSON.stringify(report, null, 2), "utf-8");
})();
NODE

  VIEWER_URL="$URL" REPORT_PATH="$REPORT" npx --yes playwright node "$tmp_script"
  rm -f "$tmp_script"
else
  if [[ -x ".venv/bin/python" ]]; then
    PYTHON_BIN=".venv/bin/python"
  else
    PYTHON_BIN="python3"
  fi

  VIEWER_URL="$URL" REPORT_PATH="$REPORT" "$PYTHON_BIN" - <<'PY'
import json
import os
from datetime import datetime, timezone
from playwright.sync_api import sync_playwright

url = os.environ["VIEWER_URL"]
report_path = os.environ["REPORT_PATH"]

report = {
    "url": url,
    "started_at": datetime.now(timezone.utc).isoformat(),
    "timeline": [],
    "console": [],
    "selected_card": None,
    "model_status": None,
    "warnings": [],
}

with sync_playwright() as p:
    browser = p.chromium.launch(headless=True)
    page = browser.new_page()
    page.on("console", lambda msg: report["console"].append({"type": msg.type, "text": msg.text}))

    report["timeline"].append({"step": "goto", "status": "start"})
    page.goto(url, wait_until="networkidle")
    report["timeline"].append({"step": "goto", "status": "ok"})

    page.wait_for_selector("#summary", timeout=10000)
    summary = page.locator("#summary").inner_text()
    report["timeline"].append({"step": "summary", "status": "ok", "summary": summary})

    cards = page.locator("#gallery .card")
    card_count = cards.count()
    report["timeline"].append({"step": "count_cards", "status": "ok", "card_count": card_count})

    if card_count == 0:
      report["warnings"].append("No cards rendered in gallery")
    else:
      card = cards.first
      card_title = card.locator(".title").inner_text(timeout=5000)
      report["selected_card"] = card_title
      card.click()
      report["timeline"].append({"step": "click_first_card", "status": "ok", "card": card_title})
      page.wait_for_timeout(800)
      model_status = page.locator("#model-status").inner_text()
      report["model_status"] = model_status
      report["timeline"].append({"step": "model_status", "status": "ok", "model_status": model_status})
      if "Failed to load" in model_status:
          report["warnings"].append("Model status indicates load failure")

    browser.close()

report["finished_at"] = datetime.now(timezone.utc).isoformat()
with open(report_path, "w", encoding="utf-8") as f:
    json.dump(report, f, indent=2)
PY
fi

echo "wrote $REPORT"

if [[ "$STRICT" == "1" ]]; then
  if grep -qi '"warnings": \[' "$REPORT" && ! grep -qi '"warnings": \[\s*\]' "$REPORT"; then
    exit 1
  fi
fi
