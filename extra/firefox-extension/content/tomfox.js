// Tomfox by Harry Coal - http://harrycoal.co.uk/tomfox - harry@harrycoal.co.uk

/* ***** BEGIN LICENSE BLOCK *****
 *   Version: MPL 1.1/GPL 2.0/LGPL 2.1
 *
 * The contents of this file are subject to the Mozilla Public License Version
 * 1.1 (the "License"); you may not use this file except in compliance with
 * the License. You may obtain a copy of the License at
 * http://www.mozilla.org/MPL/
 * 
 * Software distributed under the License is distributed on an "AS IS" basis,
 * WITHOUT WARRANTY OF ANY KIND, either express or implied. See the License
 * for the specific language governing rights and limitations under the
 * License.
 *
 * The Original Code is Tomfox.
 *
 * The Initial Developer of the Original Code is
 * Harry Coal.
 * Portions created by the Initial Developer are Copyright (C) 2008
 * the Initial Developer. All Rights Reserved.
 *
 * Contributor(s): Bruno Miguel (Portuguese Translation)
 *
 * Alternatively, the contents of this file may be used under the terms of
 * either the GNU General Public License Version 2 or later (the "GPL"), or
 * the GNU Lesser General Public License Version 2.1 or later (the "LGPL"),
 * in which case the provisions of the GPL or the LGPL are applicable instead
 * of those above. If you wish to allow use of your version of this file only
 * under the terms of either the GPL or the LGPL, and not to allow others to
 * use your version of this file under the terms of the MPL, indicate your
 * decision by deleting the provisions above and replace them with the notice
 * and other provisions required by the GPL or the LGPL. If you do not delete
 * the provisions above, a recipient may use your version of this file under
 * the terms of any one of the MPL, the GPL or the LGPL.
 * 
 * ***** END LICENSE BLOCK ***** */

function createnote(){

		// Locale
		tb_strings = document.getElementById("tomfoxbundle");

		// Get Preferences
		var prefs = Components.classes["@mozilla.org/preferences-service;1"].getService(Components.interfaces.nsIPrefBranch);

		var bDisplayNote;
		if (prefs.getPrefType("extensions.tomfox.bDisplayNote") == prefs.PREF_BOOL){
		  bDisplayNote = prefs.getBoolPref("extensions.tomfox.bDisplayNote");
		}
		
		var bAddUrl;
		if (prefs.getPrefType("extensions.tomfox.bAddUrl") == prefs.PREF_BOOL){
		  bAddUrl = prefs.getBoolPref("extensions.tomfox.bAddUrl");
		}

		var bPromptForTitle;
		if (prefs.getPrefType("extensions.tomfox.bPromptForTitle") == prefs.PREF_BOOL){
		  bPromptForTitle = prefs.getBoolPref("extensions.tomfox.bPromptForTitle");
		}

		var sDefaultNotebook = prefs.getCharPref("extensions.tomfox.sDefaultNotebook");
		var sDefaultNotebook = trim(sDefaultNotebook);

		// Tomboy DBUS
		var tomboy = "dbus-send --type=method_call --print-reply --session --dest=org.gnome.Tomboy ";
		tomboy = tomboy + "/org/gnome/Tomboy/RemoteControl org.gnome.Tomboy.RemoteControl.";

		// Process
		var file = Components.classes["@mozilla.org/file/local;1"].createInstance(Components.interfaces.nsILocalFile);
		file.initWithPath("/bin/bash");
		var process = Components.classes["@mozilla.org/process/util;1"].createInstance(Components.interfaces.nsIProcess);
		process.init(file);

		var tbtitle = PageTitle();

		if (bPromptForTitle==true)
		{
		tbtitle = window.prompt(tb_strings.getString("notetitleprompt"),tbtitle,"Tomfox");
		}
		
		if(tbtitle=="")
		{
		   tbtitle = "Untitled";
		}
		
		// Limit the size of the note title
		tbtitle = tbtitle.substring(0,100);
				
		// Set Note Contents
		var tbnote = SelectedText();
		var tbURL = CurrentURL();

		// Escape text
		tbtitle = String(EscapeText(tbtitle));
		tbnote = String(EscapeText(tbnote));
		tbURL = String(EscapeText(tbURL));
		sDefaultNotebook = String(EscapeText(sDefaultNotebook));
		
		// Replace XML Characters
		tbtitle = String(ReplaceXMLchars(tbtitle));
		tbnote = String(ReplaceXMLchars(tbnote));
		tbURL = String(ReplaceXMLchars(tbURL));

		// Prepare command
		var cmd = "NAME=\"" + tbtitle +"\";";
		cmd = cmd + "FINALNAME=$NAME;";
		cmd = cmd + "NOTE=\"" + tbnote +"\";";
		cmd = cmd + "WEBURL=\"" + tbURL +"\";";
		cmd = cmd + "NOTEBOOK=\"" + sDefaultNotebook +"\";";
		cmd = cmd + "DUPES=0;";
		cmd = cmd + "URI=$(" + tomboy + "FindNote string:\"$NAME\" | grep string | awk -F\\\" '{print $2}');";
		cmd = cmd + "EXISTS=$(" + tomboy + "NoteExists string:$URI | grep boolean | awk -F\\\" '{print $1}');";
		cmd = cmd + "while [ \"$EXISTS\" = '" + "   boolean true" + "' ]; do ";
		cmd = cmd + "let \"DUPES += 1\";";
		cmd = cmd + "FINALNAME=\"$NAME #$DUPES\";";
		cmd = cmd + "URI=$(" + tomboy + "FindNote string:\"$FINALNAME\" | grep string | awk -F\\\" '{print $2}');";
		cmd = cmd + "EXISTS=$(" + tomboy + "NoteExists string:$URI | grep boolean | awk -F\\\" '{print $1}');";
		cmd = cmd + "done;";
		cmd = cmd + tomboy + "CreateNamedNote string:\"$FINALNAME\";";
		cmd = cmd + "URI=$(" + tomboy + "FindNote string:\"$FINALNAME\" | grep string | awk -F\\\" '{print $2}');";
		cmd = cmd + "CONTENTS=$FINALNAME$NOTE;";

		if (bAddUrl==true)
		{
		cmd = cmd + "XML=\"<note-content>$FINALNAME\n\n\"$NOTE\"\n\n<link:url>\"$WEBURL\"</link:url>\n\n</note-content>\";";
		}
		else
		{
		cmd = cmd + "XML=\"<note-content>$FINALNAME\n\n\"$NOTE\"\n\n</note-content>\";";
		}

		cmd = cmd + tomboy + "SetNoteContentsXml string:$URI string:\"$XML\";";

		// Default Notebook
		if (!(sDefaultNotebook == ""))
		{
		cmd = cmd + tomboy + "AddTagToNote string:$URI  string:system:notebook:\"$NOTEBOOK\";";
		}

		if (bDisplayNote==true)
		{
		cmd = cmd + tomboy + "DisplayNote string:$URI;";
		}

		// Unicode Converter
		cmd = fromUnicode(cmd);

		// Execute Command
		var args = [ "-c", cmd ];
		process.run(false, args, args.length);

}

function EscapeText(txt){

	// Escape or Replace Special Characters
	txt = txt.replace(new RegExp("\\\\","g"), " \\\\ ");
	txt = txt.replace(new RegExp("’","g"), "'");
	txt = txt.replace(new RegExp("'","g"), "\'");
	txt = txt.replace(new RegExp('"',"g"), '\\"');
	txt = txt.replace(new RegExp('`',"g"), '\\`');

	return String(txt);

}

function ReplaceXMLchars(txt){

	// Replace disallowed reserved XML characters
	txt = txt.replace(new RegExp("&","g"), "\&amp;");
	txt = txt.replace(new RegExp("<","g"), "\&lt;");
	txt = txt.replace(new RegExp(">","g"), "\&gt;");
	txt = txt.replace(new RegExp("−","g"), "\&#45;");

	return String(txt);

}

function SelectedText(){
    var focusedWindow = document.commandDispatcher.focusedWindow;
    var selection = focusedWindow.getSelection();
    return String(selection);
}

function fromUnicode(value) {
	const conv = Components.classes["@mozilla.org/intl/scriptableunicodeconverter"].createInstance(Components.interfaces.nsIScriptableUnicodeConverter);
	conv.charset = "UTF-8";
	return conv.ConvertFromUnicode(value) + conv.Finish();
}

function CurrentURL(){
    var aDocShell = document.getElementById("content").webNavigation;
    var url = aDocShell.currentURI.spec;
    var title = null;
    return url;
}

function PageTitle(){
    var aDocShell = document.getElementById("content").webNavigation;
    var url = aDocShell.currentURI.spec;
    var title = null;
    try {
      title = aDocShell.document.title;
    }catch (e) {
      title = url;
    }
    return title;
}

function trim(stringToTrim) {
	return stringToTrim.replace(/^\s+|\s+$/g,"");
}





