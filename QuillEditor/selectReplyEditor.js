//#region quill setup

const toolbarOptions = [
  ["bold", "italic", "underline", "strike"], // toggled buttons
  ["blockquote"],
  ["link", "image", "video"],

  [{ header: 1 }, { header: 2 }, { header: 3 }, { header: 4 }], // custom button values
  [{ list: "ordered" }, { list: "bullet" }, { list: "check" }],
  [{ script: "sub" }, { script: "super" }], // superscript/subscript

  [{ color: [] }, { background: [] }], // dropdown with defaults from theme

  ["clean"], // remove formatting button
];

const quill = new Quill("#editor", {
  theme: "snow",
  toolbar: true,
  placeholder: "Write an epic email",
  modules: {
    toolbar: toolbarOptions,
  },
});

function setEditorContent(html) {
  quill.root.innerHTML = html || "";
}

window.setEditorContent ??= setEditorContent;

quill.on("text-change", function () {
  if (!window.chrome.webview)
    return;

  const html = quill.root.innerHTML;
  window.chrome.webview.postMessage({ type: "html", value: html });
});

//#endregion

//#region email select

function setEmailContent(subject, { from, to, date }, email) {
  document.querySelector(".email-subject").textContent = subject;
  document.getElementById("from").textContent = from;
  document.getElementById("to").textContent = to;
  document.getElementById("date").textContent = date;
  document.querySelector(".email-body").innerHTML = email;
};

window.setEmailContent = setEmailContent;

const email = document.querySelector(".html-email-container");
const bubble = document.querySelector("#reply-bubble");

let lastSelection = "";
function showReplyBubble() {
  const selection = window.getSelection();

  if (!selection || selection.isCollapsed || !selection.toString().trim()) {
    bubble.style.display = "none";
    return;
  }

  const range = selection.getRangeAt(0);

  // Ensure selection is inside the email container
  if (!email.contains(range.commonAncestorContainer)) {
    bubble.style.display = "none";
    return;
  }
  lastSelection = selection.toString().trim();

  const rect = range.getBoundingClientRect();
  const containerRect = email.getBoundingClientRect();

  // Measure bubble (temporarily show if needed)
  bubble.style.visibility = "hidden";
  bubble.style.display = "inline-flex";
  const bubbleWidth = bubble.offsetWidth || 80;
  const bubbleHeight = bubble.offsetHeight || 30;
  bubble.style.visibility = "";

  const margin = 4; // distance from selection

  // Start: put the bubble so its *bottom* is margin px above the selection
  let top =
    rect.top - containerRect.top + email.scrollTop - bubbleHeight + margin; // <- no extra -bubbleHeight

  let left = rect.left - containerRect.left + email.scrollLeft;

  // If that would go off the top, place it *below* instead
  if (top < 0) {
    top = rect.bottom - containerRect.top + email.scrollTop + margin;
  }

  // Clamp left so it stays inside the container
  const maxLeft = email.scrollWidth - bubbleWidth - margin;
  if (left < margin) left = margin;
  if (left > maxLeft) left = maxLeft;

  bubble.style.top = `${top}px`;
  bubble.style.left = `${left}px`;
  bubble.style.display = "inline-flex";
}

function hideReplyBubble() {
  bubble.style.display = "none";
}

// Show bubble when mouse up or key up (for Shift+Arrows selection)
email.addEventListener("mouseup", () => {
  setTimeout(showReplyBubble, 0); // wait for selection to update
});

email.addEventListener("keyup", () => {
  setTimeout(showReplyBubble, 0);
});

// Hide if you click outside the email or clear selection
document.addEventListener("mousedown", (e) => {
  if (!email.contains(e.target) && e.target !== bubble) {
    hideReplyBubble();
  }
});

// Example reply action
bubble.addEventListener("click", () => {
  const text = lastSelection.trim();
  const lines = text.split(/\r?\n/);

  // Where to insert (end of editor)
  let index = quill.getLength() - 1;

  lines.forEach((line) => {
    // insert the line + newline
    quill.insertText(index, line + "\n");
    // format that line as blockquote
    quill.formatLine(index, line.length + 1, "blockquote", true);
    index += line.length + 1;
  });

  // Add a blank line after the quote
  quill.insertText(quill.getLength() - 1, "\n");
  quill.focus();

  window.getSelection().removeAllRanges();
  lastSelection = "";
});

//#endregion
