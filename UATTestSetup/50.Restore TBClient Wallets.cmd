title Restoring Source wallet for Bitcoin network
curl -X POST --header "Content-Type: application/json-patch+json" --header "Accept: application/json" -d "{ \"mnemonic\": \"hospital icon six grace draw bronze talk silly ridge coral divide immense\", \"password\": \"node\", \"network\": \"RegTest\",        \"folderPath\": null, \"name\": \"Source\" }" "http://localhost:19100/api/Wallet/create"

title Restoring Source wallet for Stratis network
curl -X POST --header "Content-Type: application/json-patch+json" --header "Accept: application/json" -d "{ \"mnemonic\": \"hospital icon six grace draw bronze talk silly ridge coral divide immense\", \"password\": \"node\", \"network\": \"StratisRegTest\", \"folderPath\": null, \"name\": \"Source\" }" "http://localhost:19101/api/Wallet/create"


title Restoring Destination wallet for Bitcoin network
curl -X POST --header "Content-Type: application/json-patch+json" --header "Accept: application/json" -d "{ \"mnemonic\": \"stock depend dizzy beyond display indoor diet bridge tissue comic steel script\", \"password\": \"node\", \"network\": \"RegTest\",        \"folderPath\": null, \"name\": \"Destination\" }" "http://localhost:19100/api/Wallet/create"
title Restoring Destination wallet for Stratis network
curl -X POST --header "Content-Type: application/json-patch+json" --header "Accept: application/json" -d "{ \"mnemonic\": \"stock depend dizzy beyond display indoor diet bridge tissue comic steel script\", \"password\": \"node\", \"network\": \"StratisRegTest\", \"folderPath\": null, \"name\": \"Destination\" }" "http://localhost:19101/api/Wallet/create"
