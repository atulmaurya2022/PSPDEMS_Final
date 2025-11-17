/**
 * @license Copyright (c) 2003-2019, CKSource - Frederico Knabben. All rights reserved.
 * For licensing, see https://ckeditor.com/legal/ckeditor-oss-license
 */

CKEDITOR.editorConfig = function( config ) {
	// Define changes to default configuration here. For example:
	// config.language = 'fr';
	// config.uiColor = '#AADC6E';
	config.toolbarGroups = [
		{ name: 'clipboard', groups: [ 'undo', 'clipboard' ] },
		{ name: 'basicstyles', groups: [ 'basicstyles', 'cleanup' ] },
		//{ name: 'document', groups: [ 'mode', 'doctools', 'document' ] },
		{ name: 'editing', groups: [ 'find', 'selection', 'spellchecker', 'editing' ] },
		{ name: 'insert', groups: [ 'insert','newline' ] },
		{ name: 'styles', groups: [ 'styles' ] },
		{ name: 'forms', groups: [ 'forms' ] },
		{ name: 'colors', groups: [ 'colors' ] },
		{ name: 'paragraph', groups: [ 'align', 'list', 'indent', 'blocks', 'bidi', 'paragraph' ] },
		{ name: 'links', groups: [ 'links' ] },
		{ name: 'tools', groups: [ 'tools' ] },
        { name: 'Others', groups: ['PasteText', 'Paste', 'NewPage', 'Preview', 'Print', 'Image', 'HorizontalRule', 'PageBreak', 'Styles', 'PasteFromWord', 'SelectAll', 'BidiLtr', 'BidiRtl', 'Format'] },
        
        {
            name: 'others', groups: ['Maximize','others' ] }, 
	];

    config.removeButtons = 'CopyFormatting,RemoveFormat,Source,Templates,Scayt,Form,Checkbox,Radio,TextField,Textarea,Select,Button,ImageButton,HiddenField,Strike,Blockquote,CreateDiv,Language,Link,Unlink,Anchor,Flash,Smiley,Iframe,About,ShowBlocks,Save,Replace';

	//config.uiColor = '#ffffff';
    CKEDITOR.config.pasteFromWordRemoveFontStyles = false;
    CKEDITOR.config.pasteFromWordRemoveStyles = false;
    config.extraPlugins = 'lite,autogrow,newline';
	
	// config.extraPlugins = 'doNothing';

	//config.height = 600;
    config.autoGrow_onStartup = true;
};
