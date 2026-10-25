#!/bin/sh

set -eu

assembly_path=''
dotnet_path=''
process_id=''
request=''
log_path=''

while [ "$#" -gt 0 ]; do
    case "$1" in
        --dotnet)
            dotnet_path=$2
            shift 2
            ;;
        --assembly)
            assembly_path=$2
            shift 2
            ;;
        --pid)
            process_id=$2
            shift 2
            ;;
        --request)
            request=$2
            shift 2
            ;;
        --log)
            log_path=$2
            shift 2
            ;;
        *)
            echo "Unknown argument: $1" >&2
            exit 2
            ;;
    esac
done

case "$dotnet_path" in
    /*) ;;
    *)
        echo "dotnet path must be absolute: $dotnet_path" >&2
        exit 2
        ;;
esac

case "$assembly_path" in
    /*) ;;
    *)
        echo "Assembly path must be absolute: $assembly_path" >&2
        exit 2
        ;;
esac

script_directory=$(CDPATH= cd -P "$(dirname "$0")" && pwd)
patcher_path="$script_directory/KFAssemblyPatcher.dll"
if [ ! -f "$patcher_path" ]; then
    printf '%s %s\n' "$(date -u '+%Y-%m-%dT%H:%M:%SZ')" "KF assembly patcher is missing: $patcher_path" >> "$log_path"
    exit 4
fi

if [ ! -x "$dotnet_path" ]; then
    printf '%s %s\n' "$(date -u '+%Y-%m-%dT%H:%M:%SZ')" "Verified dotnet host is missing or not executable: $dotnet_path" >> "$log_path"
    exit 5
fi

exec "$dotnet_path" "$patcher_path" \
    --assembly "$assembly_path" \
    --pid "$process_id" \
    --request "$request" \
    --log "$log_path"
