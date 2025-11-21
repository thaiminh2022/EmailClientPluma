const toolbarOptions = [
    ['bold', 'italic', 'underline', 'strike'],        // toggled buttons
    ['blockquote', 'code-block'],
    ['link', 'image', 'video'],

    [{ 'header': 1 }, { 'header': 2 }, { 'header': 3 }, { 'header': 4 }],               // custom button values
    [{ 'list': 'ordered' }, { 'list': 'bullet' }, { 'list': 'check' }],
    [{ 'script': 'sub' }, { 'script': 'super' }],      // superscript/subscript
    [{ 'direction': 'rtl' }],                         // text direction

    [{ 'size': ['small', false, 'large', 'huge'] }],  // custom dropdown

    [{ 'color': [] }, { 'background': [] }],          // dropdown with defaults from theme
    [{ 'font': [] }],
    [{ 'align': [] }],

    ['clean']                                         // remove formatting button
];

const quill = new Quill('#editor', {
    theme: 'snow',
    toolbar: true,
    placeholder: "Write an epic email",
    modules: {
        toolbar: toolbarOptions
    }
});

function getEditorContent() {
    return quill.root.innerHTML;
}

function setEditorContent(html) {
    quill.root.innerHTML = html;
}

window.getEditorContent = getEditorContent;
window.setEditorContent = setEditorContent;