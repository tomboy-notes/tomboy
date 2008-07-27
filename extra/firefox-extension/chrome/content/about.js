
function openLink(aPage) {
	
	// Open page in new tab
	var wm = Components.classes["@mozilla.org/appshell/window-mediator;1"].getService();
    	var wmed = wm.QueryInterface(Components.interfaces.nsIWindowMediator);
    
	var win = wmed.getMostRecentWindow("navigator:browser");
	if (!win)
    		win = window.openDialog("chrome://browser/content/browser.xul", "_blank", "chrome,all,dialog=no", aPage, null, null);
	else {
    	var content = win.document.getElementById("content");
    	content.selectedTab = content.addTab(aPage);	
    	}
	
}
