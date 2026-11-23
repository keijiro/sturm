#!/bin/zsh
# Usage: AgentScripts/shoot.sh <tag> <preset indices...>
# Assumes Play Mode is running. Selects each preset, waits, and captures.
P=/Users/keijiro/Projects/sturm
tag=$1; shift
for i in "$@"; do
  unity command script run --file AgentScripts/Select.cs --entry Select.Preset --args "[$i]" --project-path $P >/dev/null 2>&1
  sleep ${WAIT:-6}
  unity command capture game_view --output-path Screenshots/${tag}-$i.png --project-path $P 2>&1 | grep savedPath
done
