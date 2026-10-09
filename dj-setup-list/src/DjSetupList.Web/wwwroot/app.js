const state = {
  currentList: null,
  fileName: null,
};

const views = [...document.querySelectorAll(".view")];

function show(name) {
  views.forEach((v) => v.classList.toggle("active", v.id === `view-${name}`));
  if (name === "lists") loadLists();
  if (name === "sources") loadSources();
  if (name === "create") loadTemplates();
}

document.querySelectorAll("[data-nav]").forEach((el) => {
  el.addEventListener("click", () => show(el.dataset.nav));
});

async function api(path, options) {
  const res = await fetch(path, {
    headers: { "Content-Type": "application/json", ...(options?.headers || {}) },
    ...options,
  });
  const text = await res.text();
  let data = null;
  try { data = text ? JSON.parse(text) : null; } catch { data = text; }
  if (!res.ok) throw new Error(data?.error || data || res.statusText);
  return data;
}

async function loadTemplates() {
  const templates = await api("/api/templates");
  const select = document.getElementById("template-select");
  select.innerHTML = templates
    .map((t) => `<option value="${t.id}">${t.name}（${t.itemCount}項目）</option>`)
    .join("");
}

document.getElementById("create-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const fd = new FormData(e.target);
  const body = Object.fromEntries(fd.entries());
  const created = await api("/api/lists", { method: "POST", body: JSON.stringify(body) });
  state.fileName = created.fileName;
  state.currentList = created.list;
  renderDetail();
  show("detail");
});

async function loadLists() {
  const lists = await api("/api/lists");
  const root = document.getElementById("lists");
  if (!lists.length) {
    root.innerHTML = `<p class="empty">まだリストがありません。「リストを作る」から作成してください。</p>`;
    return;
  }
  root.innerHTML = lists
    .map(
      (l) => `
      <article class="list-row" data-file="${l.fileName}">
        <div>
          <strong>${escapeHtml(l.eventName || "(無題)")}</strong>
          <div class="meta">${escapeHtml(l.venue || "会場未設定")} · ${l.templateId} · 音源 ${l.musicTrackCount}曲</div>
        </div>
        <div class="meta">${l.checkedItems}/${l.totalItems}</div>
      </article>`
    )
    .join("");
  root.querySelectorAll(".list-row").forEach((row) => {
    row.addEventListener("click", async () => {
      state.fileName = row.dataset.file;
      state.currentList = await api(`/api/lists/${encodeURIComponent(state.fileName)}`);
      renderDetail();
      show("detail");
    });
  });
}

function renderDetail() {
  const list = state.currentList;
  const root = document.getElementById("detail");
  document.getElementById("export-md").href = `/api/lists/${encodeURIComponent(state.fileName)}/export?format=md`;

  const categories = list.categories
    .map((cat) => {
      const items = cat.items
        .map(
          (item) => `
          <button type="button" class="item-row" data-item="${item.id}">
            <span style="display:flex;align-items:flex-start">
              <span class="check ${item.checked ? "on" : ""}">${item.checked ? "✓" : ""}</span>
              <span>
                <strong>${escapeHtml(item.name)}</strong>
                ${item.essential ? "" : ' <span class="badge">任意</span>'}
                ${item.note ? `<div class="meta">${escapeHtml(item.note)}</div>` : ""}
              </span>
            </span>
          </button>`
        )
        .join("");
      return `<div class="category"><h3>${escapeHtml(cat.title)}</h3><div class="stack">${items}</div></div>`;
    })
    .join("");

  const music =
    list.music?.length > 0
      ? list.music
          .map(
            (set) => `
        <div class="panel">
          <strong>${escapeHtml(set.playlistName || set.source)}</strong>
          <div class="meta">${set.source} · ${set.trackCount}曲 · オフライン可 ${set.offlineReadyCount}</div>
          ${(set.tracks || [])
            .slice(0, 30)
            .map(
              (t) =>
                `<div class="track"><span>${escapeHtml(t.artist ? `${t.artist} — ${t.title}` : t.title)}</span><span class="meta">${t.offlineReady ? "offline" : "stream"}</span></div>`
            )
            .join("")}
        </div>`
          )
          .join("")
      : `<p class="empty">音源はまだ添付されていません。</p>`;

  root.innerHTML = `
    <div class="panel">
      <h2>${escapeHtml(list.eventName || list.templateName)}</h2>
      <div class="meta">${escapeHtml(list.venue || "")} · ${list.eventDate || "日付未設定"} · 進捗 ${list.checkedItems}/${list.totalItems} · 必須残 ${list.essentialMissing}</div>
    </div>
    ${categories}
    <h3>音源セット</h3>
    <div class="stack">${music}</div>
  `;

  root.querySelectorAll("[data-item]").forEach((btn) => {
    btn.addEventListener("click", async () => {
      state.currentList = await api(`/api/lists/${encodeURIComponent(state.fileName)}/toggle`, {
        method: "POST",
        body: JSON.stringify({ itemId: btn.dataset.item }),
      });
      renderDetail();
    });
  });
}

document.getElementById("open-attach").addEventListener("click", () => {
  document.getElementById("attach-dialog").showModal();
});

document.getElementById("attach-submit").addEventListener("click", async () => {
  const form = document.getElementById("attach-form");
  const fd = new FormData(form);
  const body = Object.fromEntries(fd.entries());
  state.currentList = await api(`/api/lists/${encodeURIComponent(state.fileName)}/music`, {
    method: "POST",
    body: JSON.stringify(body),
  });
  document.getElementById("attach-dialog").close();
  renderDetail();
});

document.getElementById("search-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const fd = new FormData(e.target);
  const source = fd.get("source");
  const q = fd.get("q") || "";
  const root = document.getElementById("search-results");
  root.innerHTML = `<p class="empty">検索中…</p>`;
  try {
    const result = await api(`/api/music/search?source=${encodeURIComponent(source)}&q=${encodeURIComponent(q)}`);
    const playlists = (result.playlists || [])
      .map(
        (p) => `
        <article class="panel">
          <strong>${escapeHtml(p.name)}</strong>
          <div class="meta">${p.source} · ${p.trackCount ?? "?"}曲</div>
          <div class="meta">${escapeHtml(p.id)}</div>
          ${state.fileName ? `<button type="button" class="btn ghost attach-pl" data-id="${escapeAttr(p.id)}" data-name="${escapeAttr(p.name)}" data-source="${escapeAttr(source)}">このリストに添付</button>` : ""}
        </article>`
      )
      .join("");
    const tracks = (result.tracks || [])
      .map(
        (t) => `
        <div class="track">
          <span>${escapeHtml(t.artist ? `${t.artist} — ${t.title}` : t.title)}</span>
          <span class="meta">${t.offlineReady ? "offline" : t.source}</span>
        </div>`
      )
      .join("");
    root.innerHTML = `
      ${result.warning ? `<p class="hint">${escapeHtml(result.warning)}</p>` : ""}
      <h3>プレイリスト</h3>
      <div class="stack">${playlists || '<p class="empty">なし</p>'}</div>
      <h3>トラック</h3>
      <div class="panel">${tracks || '<p class="empty">なし</p>'}</div>
    `;
    root.querySelectorAll(".attach-pl").forEach((btn) => {
      btn.addEventListener("click", async () => {
        state.currentList = await api(`/api/lists/${encodeURIComponent(state.fileName)}/music`, {
          method: "POST",
          body: JSON.stringify({
            source: btn.dataset.source,
            playlist: btn.dataset.id,
            name: btn.dataset.name,
          }),
        });
        show("detail");
        renderDetail();
      });
    });
  } catch (err) {
    root.innerHTML = `<p class="empty">${escapeHtml(err.message)}</p>`;
  }
});

async function loadSources() {
  const statuses = await api("/api/music/sources");
  document.getElementById("sources").innerHTML = statuses
    .map((s) => {
      const badge = s.available ? "ok" : s.configured ? "warn" : "bad";
      const label = s.available ? "利用可" : s.configured ? "設定あり" : "未設定";
      return `
        <article class="panel">
          <div style="display:flex;justify-content:space-between;gap:1rem;align-items:center">
            <strong>${escapeHtml(s.displayName)}</strong>
            <span class="badge ${badge}">${label}</span>
          </div>
          <p class="meta">${escapeHtml(s.message || "")}</p>
          ${s.authUrlHint ? `<p class="meta"><a href="${escapeAttr(s.authUrlHint)}" target="_blank" rel="noreferrer">セットアップ資料</a></p>` : ""}
        </article>`;
    })
    .join("");
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}
function escapeAttr(value) {
  return escapeHtml(value).replaceAll("'", "&#39;");
}

show("home");
loadTemplates().catch(console.error);
