<?xml version='1.0'?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
		xmlns:tomboy="http://beatniksoftware.com/tomboy"
		xmlns:size="http://beatniksoftware.com/tomboy/size"
		xmlns:link="http://beatniksoftware.com/tomboy/link"
                version='1.0'>

<xsl:output method="html" indent="no" />
<xsl:preserve-space elements="*" />

<xsl:param name="font" />
<xsl:param name="export-linked" />
<xsl:param name="export-linked-all" />
<xsl:param name="root-note" />

<xsl:param name="newline" select="'&#xA;'" />

<xsl:template match="/">
	<html>
	<head>
	<title><xsl:value-of select="/tomboy:note/tomboy:title" /></title>
	<style type="text/css">
	body { <xsl:value-of select="$font" /> }
	h1 { font-size: xx-large;
     	     font-weight: bold;
     	     color: #204A87;
     	     text-decoration: underline; }
	div.note { overflow: auto;
		   position: relative;
		   border: 1px solid black;
		   display: block;
		   padding: 5pt;
		   margin: 5pt; 
		   white-space: -moz-pre-wrap; /* Mozilla */
 	      	   white-space: -pre-wrap;     /* Opera 4 - 6 */
 	      	   white-space: -o-pre-wrap;   /* Opera 7 */
 	      	   white-space: pre-wrap;      /* CSS3 */
 	      	   word-wrap: break-word;      /* IE 5.5+ */ }
	</style>
	</head>
	<body>

	<xsl:apply-templates select="tomboy:note"/>

	</body>
	</html>
</xsl:template>

<xsl:template match="tomboy:note">
	<xsl:apply-templates select="tomboy:text"/>
</xsl:template>

<xsl:template match="tomboy:text">
	<div class="note" 
	     id="{/tomboy:note/tomboy:title}"
	     style="width:{/tomboy:note/tomboy:width};">
		<a name="#{/tomboy:note/tomboy:title}" />
		<xsl:apply-templates select="node()" />
	</div>
	
	<xsl:if test="$export-linked and ((not($export-linked-all) and /tomboy:note/tomboy:title/text() = $root-note) or $export-linked-all)">
		<!--
			Loop through all the links contained in this note and
			check to see if they've already been loaded/processed
			(skip them if they have).
		-->
		<xsl:for-each select=".//link:internal/text()">
		
			<xsl:variable name="link-already-processed">
				<xsl:call-template name="note-already-loaded">
					<xsl:with-param name="title" select="."/>
				</xsl:call-template>
			</xsl:variable>
			
			<xsl:if test="contains($link-already-processed, 'NO')">
				<!-- Load in the linked note's XML for processing. -->
				<xsl:apply-templates select="document(.)/node()"/>
			</xsl:if>

		</xsl:for-each>
	</xsl:if>
</xsl:template>

<xsl:template match="tomboy:note/tomboy:text/*[1]/text()[1]">
	<h1><xsl:value-of select="substring-before(., $newline)"/></h1>
	<xsl:value-of select="substring-after(., $newline)"/>
</xsl:template>

<xsl:template match="tomboy:bold">
	<b><xsl:apply-templates select="node()"/></b>
</xsl:template>

<xsl:template match="tomboy:italic">
	<i><xsl:apply-templates select="node()"/></i>
</xsl:template>

<xsl:template match="tomboy:strikethrough">
	<strike><xsl:apply-templates select="node()"/></strike>
</xsl:template>

<xsl:template match="tomboy:highlight">
	<span style="background:yellow"><xsl:apply-templates select="node()"/></span>
</xsl:template>

<xsl:template match="tomboy:datetime">
	<span style="font-style:italic;font-size:small;color:#888A85">
		<xsl:apply-templates select="node()"/>
	</span>
</xsl:template>

<xsl:template match="size:small">
	<span style="font-size:small"><xsl:apply-templates select="node()"/></span>
</xsl:template>

<xsl:template match="size:large">
	<span style="font-size:large"><xsl:apply-templates select="node()"/></span>
</xsl:template>

<xsl:template match="size:huge">
	<span style="font-size:xx-large"><xsl:apply-templates select="node()"/></span>
</xsl:template>

<xsl:template match="link:broken">
	<span style="color:#555753;text-decoration:underline">
		<xsl:value-of select="node()"/>
	</span>
</xsl:template>

<xsl:template match="link:internal">
	<a style="color:#204A87" href="#{document(node())/tomboy:note/tomboy:title}">
		<xsl:value-of select="node()"/>
	</a>
</xsl:template>

<xsl:template match="link:url">
	<a style="color:#3465A4" href="{node()}"><xsl:value-of select="node()"/></a>
</xsl:template>

<xsl:template match="tomboy:list">
	<ul>
		<xsl:apply-templates select="tomboy:list-item" />
	</ul>
</xsl:template>

<xsl:template match="tomboy:list-item">
	<li>
		<xsl:if test="normalize-space(text()) = ''">
			<xsl:attribute name="style">list-style-type: none</xsl:attribute>
		</xsl:if>
		<xsl:attribute name="dir">
			<xsl:value-of select="@dir"/>
		</xsl:attribute>
		<xsl:apply-templates select="node()" />
	</li>
</xsl:template>

<!--
	The "note-title" key keeps a list of all note titles that have been
	processed.  Each time a note's XML is loaded, its title is added to this
	key automatically.  This is used to prevent including the text of a note
	more than once if "export-linked" is true and a recursive link to a note
	is found.
-->
<xsl:key name="note-title" match="tomboy:note" use="tomboy:title" />

<xsl:template name="note-already-loaded">
	<xsl:param name="title" />
	<xsl:choose>
		<xsl:when test="key('note-title', $title)">
			YES
		</xsl:when>
		<xsl:otherwise>
			NO
		</xsl:otherwise>
	</xsl:choose>
</xsl:template>

<!-- Evolution.dll Plugin -->
<xsl:template match="link:evo-mail">
	<a href="{./@uri}">
		<img alt="Open Email Link" width="16" height="10" border="0">
			<!-- Inline Base64 encoded stock_mail.png =) -->
			<xsl:attribute name="src">data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAKCAYAAAC9vt6cAAAABmJLR0QA/wD/AP+gvaeTAAAACXBI WXMAAAsQAAALEAGtI711AAAAB3RJTUUH1QkeAjYaRAvZgAAAALxJREFUKM+NkjGKw1AMRN+GhRS/ 2xP4EHZr0E1UxFVuoiKdikCKfxMfwKdw+3t1gb/F4hASe50BgZjRDEII/jAAtWmaCnxSAy+oZlYj YrfMbAkB4GsJiAjcnfPpRNzvrCHnjIjQdd3De3geUFX8diMdj6tmVX3jD6+EquLXKz9p37waANC2 LRfPpJTIOdP3PXuoEVFLKdXMaills5+m6f8jbq26dcTvRXR3RIR5njcDRIRxHFe14cMHenukX9eX mbvfl0q9AAAAAElFTkSuQmCC</xsl:attribute>
		</img>
		<xsl:value-of select="node()"/>
	</a>
</xsl:template>

<!-- FixedWidth.dll Plugin -->
<xsl:template match="tomboy:monospace">
	<span style="font-family:monospace"><xsl:apply-templates select="node()"/></span>
</xsl:template>

</xsl:stylesheet>

