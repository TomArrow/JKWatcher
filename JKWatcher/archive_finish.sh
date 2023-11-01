#/bin/bash

#use this script if upload fails with archive.sh. change up "date" to match generated files and execute.

mkdir -p old
date="2021-09-01_03-00-45"
filename=$date.zip
filenameScript=$date-demoCuts.sh

# upload the .zip file to archive.org
curl --fail --location --header 'x-amz-auto-make-bucket:0' \
--header 'x-archive-meta-language:eng' \
--header "authorization: LOW $accesskey:$secret" \
--upload-file "$filename" \
"http://s3.us.archive.org/democuts/$filename"

# if successful, move .zip and .sh file to old/ folder
retVal=$?
echo $retVal\n
if [ $retVal -eq 0 ]; then
    mv $filename old/$filename
	if test -f "$filenameScript"; then # would be strange if it didn't exist since we made it above, but let's double check.
		mv "$filenameScript" "old/$filenameScript"
	fi
fi
read -r -n1