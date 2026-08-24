#!/usr/bin/env bash
# Refresh the embedded API contract from the live server, then surface any SDK gaps.
#
# The embedded file must stay byte-identical to the live document: the scheduled
# contract-drift workflow diffs the two raw files, so never hand-edit or annotate
# contract/integrations-v1.yaml.
set -euo pipefail
cd "$(dirname "$0")/.."

SPEC_URL="${SPEC_URL:-https://courses.trainingandetrackingsolutions.com/api/integrations/v1/openapi.yaml}"
curl -fsS --retry 3 "$SPEC_URL" -o contract/integrations-v1.yaml

if git diff --quiet -- contract/integrations-v1.yaml; then
  echo "Already in sync with $SPEC_URL"
else
  echo "Contract refreshed from $SPEC_URL:"
  git --no-pager diff --stat -- contract/integrations-v1.yaml
  echo
  echo "Next: dotnet test   (contract parity tests flag any operations or models the SDK now misses)"
fi
