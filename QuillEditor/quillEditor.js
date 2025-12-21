const toolbarOptions = [
    ["bold", "italic", "underline", "strike"], // toggled buttons
    ["blockquote", "link"],

    [{ header: 1 }, { header: 2 }, { header: 3 }, { header: 4 }], // custom button values
    [{ list: "ordered" }, { list: "bullet" }, { list: "check" }],
    [{ script: "sub" }, { script: "super" }], // superscript/subscript

    [{ color: [] }, { background: [] }], // dropdown with defaults from theme

    ["clean"], // remove formatting button
];

const quill = new Quill('#editor', {
    theme: 'snow',
    toolbar: true,
    placeholder: "Write an epic email",
    modules: {
        toolbar: toolbarOptions
    }
});

function setEditorContent(html) {
    quill.root.innerHTML = html || "";
}

window.setEditorContent = setEditorContent;

quill.on('text-change', function () {
    const html = quill.root.innerHTML;
    window.chrome.webview.postMessage({ type: 'html', value: html });
});

const host = document.getElementById("editor-container");
const mq = window.matchMedia("(prefers-color-scheme: dark)");

function syncTheme(e) {
    const isDark = e?.matches ?? mq.matches;
    host.classList.toggle("quill-dark", isDark);
}

syncTheme();
if (mq.addEventListener) mq.addEventListener("change", syncTheme);
