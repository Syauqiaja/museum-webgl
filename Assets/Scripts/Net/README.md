# Museum.Net — generated schema

`Schema/*.cs` is **generated from the server**, not written here. It is the C# mirror
of `src/rooms/schema/*.ts` in the `museum-minigames` backend repo, and the Colyseus
SDK uses it to decode state patches. Editing a file in `Schema/` by hand desyncs the
decoder from the server and the symptom is garbled state, not a compile error.

Regenerate after any server schema change (run from the server repo):

```bash
npx schema-codegen src/rooms/schema/DakonState.ts  --csharp --namespace Museum.Net.Schema --output /tmp/schemagen
npx schema-codegen src/rooms/schema/EgrangState.ts --csharp --namespace Museum.Net.Schema --output /tmp/schemagen
cp /tmp/schemagen/*.cs "<this repo>/Assets/Scripts/Net/Schema/"
```

(The generator writes each state's dependencies too, so those two commands cover
`BaseGameState`, `BasePlayer`, `DakonSeed` and `DakonStore`. Generating straight into
a path containing spaces fails — hence the temp directory.)

Field order matters: Colyseus encodes by field index, so client and server must be
generated from the same schema revision. If they drift, players see a board that does
not match the server's.
