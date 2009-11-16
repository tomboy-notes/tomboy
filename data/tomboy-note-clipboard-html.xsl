<?xml version='1.0'?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
		xmlns:tomboy="http://beatniksoftware.com/tomboy"
		xmlns:size="http://beatniksoftware.com/tomboy/size"
		xmlns:link="http://beatniksoftware.com/tomboy/link"
                version='1.0'>

<xsl:output method="html" indent="no" />
<xsl:preserve-space elements="*" />

<xsl:param name="font" />
<xsl:param name="newline" select="'&#xA;'" />

<xsl:template match="/">
	<html><body>

    <xsl:apply-templates select="note"/>

	</body></html>
</xsl:template>

<xsl:template match="text()">
   <xsl:call-template name="softbreak"/>
</xsl:template>

<xsl:template name="softbreak">
	<xsl:param name="text" select="."/>
	<xsl:choose>
		<xsl:when test="contains($text, $newline)">
			<xsl:value-of select="substring-before($text, $newline)"/>
			<br/>
			<xsl:call-template name="softbreak">
				<xsl:with-param name="text" select="substring-after($text, $newline)"/>
			</xsl:call-template>
		</xsl:when>

		<xsl:otherwise>
			<xsl:value-of select="$text"/>
		</xsl:otherwise>
	</xsl:choose>
</xsl:template>

<xsl:template match="note-content">
    <xsl:apply-templates select="node()" />
</xsl:template>

<xsl:template match="bold">
	<b><xsl:apply-templates select="node()"/></b>
</xsl:template>

<xsl:template match="italic">
	<i><xsl:apply-templates select="node()"/></i>
</xsl:template>

<xsl:template match="strikethrough">
	<strike><xsl:apply-templates select="node()"/></strike>
</xsl:template>

<xsl:template match="highlight">
	<span style="background:yellow"><xsl:apply-templates select="node()"/></span>
</xsl:template>

<xsl:template match="datetime">
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
    <xsl:value-of select="node()"/>
</xsl:template>

<xsl:template match="link:internal">
    <xsl:value-of select="node()"/>
</xsl:template>

<xsl:template match="link:url">
	<a style="color:#3465A4" href="{node()}"><xsl:value-of select="node()"/></a>
</xsl:template>

<xsl:template match="list">
	<ul>
		<xsl:apply-templates select="list-item" />
	</ul>
</xsl:template>

<xsl:template match="list-item">
	<li>
		<xsl:if test="normalize-space(text()) = '' and count(list) = 1 and count(*) = 1">
			<xsl:attribute name="style">list-style-type: none</xsl:attribute>
		</xsl:if>
		<xsl:attribute name="dir">
			<xsl:value-of select="@dir"/>
		</xsl:attribute>
		<xsl:apply-templates select="node()" />
	</li>
</xsl:template>

<!-- Evolution.dll Plugin -->
<xsl:template match="link:evo-mail">
    <xsl:value-of select="node()"/>
</xsl:template>

<!-- FixedWidth.dll Plugin -->
<xsl:template match="monospace">
	<span style="font-family:monospace"><xsl:apply-templates select="node()"/></span>
</xsl:template>

<!-- Bugzilla.dll Plugin -->
<xsl:template match="link:bugzilla">
	<a href="{@uri}"><xsl:value-of select="node()" /></a>
</xsl:template>

</xsl:stylesheet>

