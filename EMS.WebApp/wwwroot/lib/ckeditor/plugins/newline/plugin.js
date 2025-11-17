CKEDITOR.plugins.add('newline', {
    icons: "newline",
    init: function (editor) {
        editor.ui.addButton('newline', {
            label: "Insert New Line",
            command: "insertNewLine",
            toolbar: "insert"
        });

        editor.addCommand('insertNewLine', {
            exec: function (editor) {
                editor.insertHtml('<p></p>');
            }
        });
    }
});