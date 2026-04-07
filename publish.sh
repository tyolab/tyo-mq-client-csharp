#!/bin/bash

API_KEY=`cat .env | grep API_KEY= | cut -d= -f2`
PACKAGE_NAME=$1

dotnet nuget push $PACKAGE_NAME --api-key $API_KEY --source https://api.nuget.org/v3/index.json