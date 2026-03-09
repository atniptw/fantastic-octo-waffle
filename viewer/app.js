async function loadMetadata() {
  const summary = document.getElementById("summary");
  const gallery = document.getElementById("gallery");
  try {
    const response = await fetch("../data/outputs/metadata.json");
    if (!response.ok) {
      summary.textContent = "No metadata found yet. Run processor first.";
      return;
    }

    const data = await response.json();
    summary.textContent = `Source: ${data.source} | hhh=${data.hhh_count} | images=${data.image_count} | parse-errors=${data.hhh_parse_errors}`;

    gallery.innerHTML = "";
    for (const item of data.images ?? []) {
      const card = document.createElement("article");
      card.className = "card";

      const image = document.createElement("img");
      image.loading = "lazy";
      image.alt = item.hhh_entry;
      image.src = `../data/outputs/${item.image}`;
      card.appendChild(image);

      const body = document.createElement("div");
      body.className = "card-body";
      body.innerHTML = `
        <h2 class="title">${item.hhh_entry}</h2>
        <p class="meta">mode=${item.render_mode ?? "unknown"} source=${item.texture_source ?? "none"}</p>
        <p class="meta">mesh=${item.mesh?.name ?? "none"}</p>
        <p class="meta">v=${item.mesh?.vertex_count ?? 0} tri=${item.mesh?.triangle_count ?? 0}</p>
      `;

      const warn = (item.warnings ?? [])[0];
      if (warn) {
        const warnLine = document.createElement("p");
        warnLine.className = "warn";
        warnLine.textContent = warn;
        body.appendChild(warnLine);
      }

      card.appendChild(body);
      gallery.appendChild(card);
    }
  } catch {
    summary.textContent = "Unable to load metadata. Run viewer via a local static server.";
  }
}

loadMetadata();
