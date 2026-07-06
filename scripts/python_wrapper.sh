#!/bin/bash
script="$1"
payload="{\"script\":\"$script\"}"
len=${#payload}
exec 3<>/dev/tcp/172.17.0.1/9998
printf 'POST /run HTTP/1.1\r\nHost: bridge\r\nContent-Type: application/json\r\nContent-Length: %d\r\nConnection: close\r\n\r\n%s' "$len" "$payload" >&3
while IFS= read -r -t 5 line <&3; do
    line=${line%$'\r'}
    [ -z "$line" ] && break
done
cat <&3
exec 3>&-
