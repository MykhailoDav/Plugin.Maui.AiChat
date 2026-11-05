#!/usr/bin/env bash
# Builds the UI test host app for a platform and runs the UI tests against it (macOS).
#
#   ./run.sh ios                            iOS simulator (iPhone 17 by default, UITEST_DEVICE to change)
#   ./run.sh android                        the running Android emulator, never a connected phone (UITEST_DEVICE
#                                           names an AVD to start instead)
#   ./run.sh maccatalyst                    the Mac (needs macOS Automation Mode, see README.md)
#   ./run.sh ios --update-baselines         save the screenshots as the new baselines
#   ./run.sh ios --filter "FullyQualifiedName~ToolApprovalTests"   run some of the tests
set -euo pipefail

usage="Usage: run.sh <ios|android|maccatalyst> [--update-baselines] [--filter <expression>]"
platform="${1:?$usage}"
shift

here="$(cd "$(dirname "$0")" && pwd)"
case "$platform" in
	ios) framework=net10.0-ios ;;
	android) framework=net10.0-android ;;
	maccatalyst) framework=net10.0-maccatalyst ;;
	*) echo "$usage" >&2; exit 1 ;;
esac

update=0
filter=()
while [[ $# -gt 0 ]]; do
	case "$1" in
		--update-baselines) update=1 ;;
		--filter) filter=(--filter "$2"); shift ;;
		*) echo "$usage" >&2; exit 1 ;;
	esac
	shift
done

# Without a named AVD, the tests go to the running emulator by its serial. A phone connected at the same time, for
# example over wireless debugging, is left alone.
if [[ "$platform" == android && -z "${UITEST_DEVICE:-}" && -z "${UITEST_UDID:-}" ]]; then
	UITEST_UDID="$(adb devices | awk '/^emulator-[0-9]+[[:space:]]+device/ { print $1; exit }')"
	if [[ -z "$UITEST_UDID" ]]; then
		echo "No Android emulator is running. Start one, or set UITEST_DEVICE to an AVD for Appium to start." >&2
		exit 1
	fi
	export UITEST_UDID
fi

if [[ ! -d "$here/node_modules" ]]; then
	(cd "$here" && npm ci)
fi

dotnet build "$here/../Plugin.Maui.AiChat.UITests.HostApp/Plugin.Maui.AiChat.UITests.HostApp.csproj" -f "$framework" -c Debug

UITEST_PLATFORM="$platform" UITEST_UPDATE_BASELINES="$update" \
	dotnet test --project "$here/Plugin.Maui.AiChat.UITests.csproj" --output Detailed "${filter[@]+"${filter[@]}"}"
