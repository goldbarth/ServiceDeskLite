cd .build/docs
npm run generate
cd ../..
dotnet run --project tools/ServiceDeskLite.DocsGen -c Release
docfx ./docs/docfx.json
