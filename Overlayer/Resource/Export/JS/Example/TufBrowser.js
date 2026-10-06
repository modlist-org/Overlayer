// TUF level browser: search tuforums.com, download, and play levels.
// Copy into JS/Script to use. Adds a "TUF" menu tab.
// Levels are saved to UserData/Overlayer/TUF/<level id>/.
//
// Ref: Quartz TUF module (api.tuforums.com v2).

const API = "https://api.tuforums.com/v2/database/levels";
const PAGE_SIZE = 25;
const ROOT = Net.DataPath + "/TUF";
const SORTS = { "Recent": "RECENT", "Difficulty": "DIFF", "Clears": "CLEARS", "Likes": "LIKES" };

const state = {
    query: "",
    sort: "Recent",
    ascending: false,
    offset: 0,
    levels: [],
    hasMore: false,
    loading: false,
    status: "Search for a level.",
    busy: {},   // level id -> "Downloading 42%" etc.
};
let tabHandle = -1;
let statusText = null;

function rebuild() {
    if (tabHandle >= 0) RebuildTab(tabHandle);
}

function setStatus(text) {
    state.status = text;
    if (statusText !== null && Unity.IsValid(statusText)) statusText.text = text;
}

function search(offset) {
    if (state.loading) return;
    state.loading = true;
    state.offset = Math.max(0, offset);
    setStatus("Loading...");
    const url = API + "?limit=" + PAGE_SIZE
        + "&offset=" + state.offset
        + "&query=" + encodeURIComponent(state.query.trim())
        + "&pguRange=" + encodeURIComponent("P1,U20")
        + "&sort=" + SORTS[state.sort] + "_" + (state.ascending ? "ASC" : "DESC")
        + "&deletedFilter=hide";
    Net.Get(url, (ok, status, body) => {
        state.loading = false;
        if (!ok) {
            setStatus("TUF request failed (" + status + "): " + String(body).slice(0, 120));
            return;
        }
        try {
            const root = JSON.parse(body);
            state.levels = (root.results || []).map(parseLevel).filter(l => l !== null);
            state.hasMore = root.hasMore === true;
            state.status = state.levels.length === 0
                ? "No levels found."
                : "Showing " + (state.offset + 1) + "-" + (state.offset + state.levels.length);
        } catch (e) {
            state.status = "Bad TUF response: " + e.message;
        }
        rebuild();
    });
}

function credits(level) {
    if (level.creator) return level.creator;
    if (level.charter) return level.charter;
    const list = (level.levelCredits || [])
        .filter(c => c.creator && c.creator.name)
        .sort((a, b) => (/charter/i.test(b.role || "") ? 1 : 0) - (/charter/i.test(a.role || "") ? 1 : 0))
        .map(c => c.creator.name);
    return list.length === 0 ? "Unknown creator" : list.slice(0, 3).join(" & ") + (list.length > 3 ? " ..." : "");
}

function parseLevel(raw) {
    if (!raw || !(raw.id > 0)) return null;
    return {
        id: raw.id,
        song: raw.song || "Unknown song",
        artist: raw.artist || "Unknown artist",
        creator: credits(raw),
        difficulty: (raw.difficulty && raw.difficulty.name) || "Unranked",
        clears: raw.clears || 0,
        likes: raw.likes || 0,
        dlLink: typeof raw.dlLink === "string" && /^https?:\/\//.test(raw.dlLink) ? raw.dlLink : null,
    };
}

function levelDir(id) {
    return ROOT + "/" + id;
}

// Same pick order as Quartz: main.adofai, then anything that isn't a backup.
function findChart(id) {
    const files = Net.FindFiles(levelDir(id), "*.adofai");
    const charts = [];
    for (let i = 0; i < files.length; i++) {
        if (!/backup/i.test(files[i])) charts.push(files[i]);
    }
    return charts.find(f => /[\\/]main\.adofai$/i.test(f)) || charts[0] || null;
}

function play(chart) {
    const controller = Clr.TryGet("scrController", "instance");
    if (controller === null || !Unity.IsValid(controller)) {
        setStatus("Go to the main menu or level select first.");
        return;
    }
    Clr.Set("GCS", "speedTrialMode", false);
    Clr.Set("GCS", "practiceMode", false);
    Clr.Call(controller, "LoadCustomLevel", chart, null, false);
}

function download(level) {
    if (state.busy[level.id]) return;
    const zip = ROOT + "/" + level.id + ".zip";
    const setBusy = text => {
        state.busy[level.id] = text;
        setStatus(level.song + ": " + text);
    };
    const done = message => {
        delete state.busy[level.id];
        setStatus(message);
        rebuild();
    };
    setBusy("Downloading...");
    Net.Download(level.dlLink, zip, (ok, error) => {
        if (!ok) return done("Download failed: " + error);
        setBusy("Extracting...");
        Net.Unzip(zip, levelDir(level.id), (ok2, error2) => {
            Clr.TryCall("System.IO.File", "Delete", zip);
            if (!ok2) return done("Extract failed: " + error2);
            const chart = findChart(level.id);
            if (chart === null) return done("No .adofai chart in the download.");
            done("Downloaded " + level.song + ".");
        });
    }, fraction => {
        if (fraction >= 0) setBusy("Downloading " + Math.round(fraction * 100) + "%");
    });
}

function build(tab) {
    tab.Header("TUF Levels");

    const bar = tab.Row();
    bar.Input("Search song, artist or creator", state.query, v => { state.query = v; });
    bar.Dropdown(Object.keys(SORTS), state.sort, v => { state.sort = v; search(0); });
    bar.Button(state.ascending ? "Ascending" : "Descending", () => {
        state.ascending = !state.ascending;
        search(0);
    });
    bar.Button("Search", () => search(0));

    statusText = tab.Text(state.status, 16);

    for (const level of state.levels) {
        const card = tab.Card(level.song + " - " + level.artist);
        card.Text(level.creator + "   |   " + level.difficulty
            + "   |   " + level.clears + " clears   |   " + level.likes + " likes", 15);
        const row = card.Row();
        const chart = findChart(level.id);
        if (chart !== null) {
            row.Button("Play", () => play(chart));
        } else if (state.busy[level.id]) {
            row.Button(state.busy[level.id], null);
        } else if (level.dlLink !== null) {
            row.Button("Download", () => download(level));
        } else {
            row.Button("No download", null);
        }
        row.Button("Open on TUF", () => Application.OpenURL("https://tuforums.com/levels/" + level.id));
    }

    if (state.levels.length > 0) {
        const pager = tab.Row();
        pager.Button("< Prev", () => { if (state.offset > 0) search(state.offset - PAGE_SIZE); });
        pager.Button("Next >", () => { if (state.hasMore) search(state.offset + PAGE_SIZE); });
    }
}

tabHandle = AddTab("TUF", build, { icon: "Gear128" });
search(0);
