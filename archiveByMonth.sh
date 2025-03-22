#/bin/bash

# collect all demos that match the default demobot naming pattern
# in particular of a specific month
# then zip them up in a flat structure (no relative paths in the zip) and upload them to archive.org
# afterwards DELETE (!) the .zip file. So don't use this to make an archive for yourself while deleting your original filess.
# this script does not delete the original files, so don't do it either.

date="2022-01" # name of the zip file
datesearch="2022-(01|January)" # regex search for the year-month part
filename=$date.zip
path="C:\path\to\demos"

# collect all the demos and zip them up.
echo $filename
if test -f "$filename"; then
	echo "$filename already exists. Going straight to upload."
else
	#7za a $filename -sse -r "$path/$date-*_ctf_*.dm_*" # I'm not using this because the wildcards aren't as good as regex and most of all 7za creates relative paths like in your original folder structure. This isn't desired here.
	RecursiveFlatZipper -o $filename -p "$path" -r "^$datesearch-[\d]+_[\d]+-[\d]+-[\d]+-.*?(ctf|nwh).*?\.dm_\d+$" # RecursiveFlatZipper is on my github too.
	
	retValZip=$?
	if [ $retValZip -eq 0 ]; then
		echo "Compression successful. Uploading."
	else
		echo "Compression errored with code $retValZip. Exiting."
		read -r -n1
		exit 1;
	fi
fi

# python ia-upload-stream.py --input-file "../$filename" --concurrency 3 --tries 999 jkctfdemos "$filename"
# python ia-upload-stream.py --input-file "../$filename" --concurrency 10 --part-size 1000M --tries 999 jkctfdemos "$filename"
# upload the .zip file to archive.org
curl --fail --location --header 'x-amz-auto-make-bucket:0' \
--header 'x-archive-meta-language:eng' \
--header "authorization: LOW $accesskey:$secret" \
--upload-file "$filename" \
--retry 1000 --retry-all-errors \
"http://s3.us.archive.org/jkctfdemos/$filename"

# if successful, delete the .zip file.
retVal=$?
echo $retVal\n
if [ $retVal -eq 0 ]; then
    rm $filename
fi
read -r -n1