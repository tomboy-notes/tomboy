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
     	     color: red;
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

	<xsl:if test="/tomboy:note/tomboy:title = $root-note">
		<xsl:if test="$export-linked">
			<xsl:apply-templates select="document(.//link:internal/text())/node()" />
		</xsl:if>
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
	<span style="font-style:italic;font-size:small;color:grey">
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
	<span style="color:silver;text-decoration:underline">
		<xsl:value-of select="node()"/>
	</span>
</xsl:template>

<xsl:template match="link:internal">
	<a style="color:red" href="#{document(node())/tomboy:note/tomboy:title}">
		<xsl:value-of select="node()"/>
	</a>
</xsl:template>

<xsl:template match="link:url">
	<a href="{node()}"><xsl:value-of select="node()"/></a>
</xsl:template>

<!-- Evolution.dll Plugin -->
<xsl:template match="link:evo-mail">
	<a href="{./@uri}"><xsl:value-of select="node()"/></a>
</xsl:template>

<!-- FixedWidth.dll Plugin -->
<xsl:template match="tomboy:monospace">
	<span style="font-family:monospace"><xsl:apply-templates select="node()"/></span>
</xsl:template>

</xsl:stylesheet>

