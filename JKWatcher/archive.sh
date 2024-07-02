#/bin/bash

mkdir -p old
date="$(date +"%Y-%m-%d_%H-%M-%S")"
filename=$date.zip
filenameScript=$date-demoCuts.sh

# Check if demoCuts.sh exists. If so, rename it.
if test -f "demoCuts.sh"; then
	mv "demoCuts.sh" "$filenameScript"
else
	# otherwise, quit
	echo "demoCuts.sh not found"
	read -r -n1
	exit 1;
fi

# execute the demo cutter script
source "$filenameScript"

# collect all the demos and zip them up.
echo $filename
7za a $filename -sse -sdel "*.dm_*"

# upload the .zip file to archive.org
curl --fail --location --header 'x-amz-auto-make-bucket:0' \
--header 'x-archive-meta-language:eng' \
--header "authorization: LOW $accesskey:$secret" \
--upload-file "$filename" \
--retry 1000 --retry-all-errors \
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