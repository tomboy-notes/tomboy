#!/bin/bash

# Use this script to keep these sources in your project in sync and updated
# with musicbrainz-sharp development. Simply run ./sync-tree.sh and update
# your project repository as directed by this script when complete

#ROOT="svn://svn.myrealbox.com/source/trunk"
ROOT="svn://anonsvn.mono-project.com/source/trunk"
MODULE=gnome-keyring-sharp
SRCDIR=src/Gnome.Keyring

FILES_ADDED=""
FILES_REMOVED=""

echo "* Checking out Sources ..."

if [ ! -d $MODULE ]; then
	svn co $ROOT/$MODULE 1>/dev/null || exit 1
fi

echo "* Syncing Sources ..."

for i in $MODULE/$SRCDIR/*.cs; do
	FILE=`basename $i`

	if [ -f $FILE ]; then 
		echo "    U $FILE"
	else 
		echo "    A $FILE"
		FILES_ADDED="yes"
	fi

	cp $MODULE/$SRCDIR/$FILE .
done

for i in *.cs; do
	FILE=`basename $i`

	if [ ! -f $MODULE/$SRCDIR/$FILE ]; then
		echo "    D $FILE"
		mv $FILE $FILE.remove
		FILES_REMOVED="yes"
	fi
done

echo "* Removing checkout"
rm -rf $MODULE

echo "* Done"

if [ ! -z $FILES_ADDED ]; then
	echo ""
	echo "Files were added since your last checkout. Please reflect this"
	echo "in your repository. Added files are flagged with 'A' above."
	echo ""
fi

if [ ! -z $FILES_REMOVED ]; then
	echo ""
	echo "Files were removed since your last checkout. Please relflect this"
	echo "in your repository. Removed files are flagged with 'R' above,"
	echo "and were given a '.remove' extension."
	echo ""
fi


