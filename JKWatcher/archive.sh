#/bin/bash
mkdir -p old
date="$(date +"%Y-%m-%d_%H-%M-%S")"
filename=$date.zip
filenameBat=$date-demoCuts.bat
echo $filename
7za a $filename -sse -sdel -r *.dm_*
curl --location --header 'x-amz-auto-make-bucket:0' \
--header 'x-archive-meta-language:eng' \
--header "authorization: LOW $accesskey:$secret" \
--upload-file $filename \
http://s3.us.archive.org/democuts/$filename
retVal=$?
echo $retVal\n
if [ $retVal -eq 0 ]; then
    mv $filename old/$filename
	if test -f "demoCuts.bat"; then
		mv "demoCuts.bat" old/$filenameBat
	fi

fi
read -r -n1