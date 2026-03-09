async function loadMetadata() {
  const status = document.getElementById("status");
  try {
    const response = await fetch("../outputs/metadata.json");
    if (!response.ok) {
      status.textContent = "No metadata found yet. Run processor first.";
      return;
    }
    const data = await response.json();
    status.textContent = JSON.stringify(data, null, 2);
  } catch {
    status.textContent = "Unable to load metadata. Run viewer via a local static server.";
  }
}

loadMetadata();
