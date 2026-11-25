#!/usr/bin/env bash
set -euo pipefail

schema_entity=${1:?SchemaEntity checkout required}
if [[ ${2:-s2sdk} == s2sdk ]]; then
  sed -i.bak \
    -e 's|tier1/utlmap.h|tier0/utlmap.h|' \
    -e 's/m_nStaticMetadataCount/m_nNumMetadata/g' \
    -e 's/m_pStaticMetadata/m_pMetadata/g' \
    -e 's/m_nFieldCount/m_nNumFields/g' \
    -e 's/m_nBaseClassCount/m_nNumBaseClasses/g' \
    -e 's/\.m_pszName/.m_pName/g' \
    "$schema_entity/schemasystem.cpp"
fi
sed -i.bak 's/NetworkStateChanged_t/NetworkStateChangedData/g' "$schema_entity/schemasystem.cpp"
if ! grep -q '#include <gametrace.h>' "$schema_entity/globaltypes.h"; then
  sed -i.bak 's|#include <platform.h>|#include <gametrace.h>\
#include <platform.h>|' "$schema_entity/globaltypes.h"
  rm "$schema_entity/globaltypes.h.bak"
fi
sed -i.bak 's/FL_PAWN_FAKECLIENT/FL_FAKECLIENT/g' "$schema_entity/CCSPlayerPawn.h"
sed -i.bak 's/FL_CONTROLLER_FAKECLIENT/FL_FAKECLIENT/g' "$schema_entity/CCSPlayerController.h"
rm "$schema_entity/"{schemasystem.cpp,CCSPlayerPawn.h,CCSPlayerController.h}.bak
