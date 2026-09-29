# Resolved condition-family synchronization

Run `dotnet run --project tests/ConditionSync/ConditionSync.csproj` with the pinned SDK.
Production ConditionSync is linked to native-like game stubs and real Newtonsoft
serialization. Compression is replaced by JSON. These checks are not a Unity,
Harmony, native save-contract or Steam integration test.

Source: ActEffect DebuffStats sets ConBuffStats.refVal (affected stat) and refVal2
(buff/debuff). BaseCondition._ints stores duration, power, those references and
flags. Subclasses add saved fields, including ConWeapon.cha, ConSong.p2, sleep,
disease, suspension and transformations. Chara.AddCondition evaluates resistance
and duration, may stack successfully while returning null, and may have several
instances sharing one type ID. The old packet lost all data except ID and power,
re-ran application on clients, skipped stacks, and removed all same-ID instances.

Host add/removal events now carry the final serialized family using the existing
LZ4Bytes/native save settings. Client reconciliation identifies instances by type
and reference values, removes only absent instances, copies native serialized
members in place for updates, and restores owner/derived state through
SetOwner(onDeserialize:true), the same entry used by Chara.InitStats. New instances
play their presentation effect; transformation renderers refresh. It does not call
AddCondition/Start again, reroll resistance, grant OnStart rewards, or toggle an
already accepted stance. Unchanged state is a no-op. Successful stacks are sent
regardless of AddCondition's return value.

Scope: existing add/remove event transport. Existing ticking, conditions modified
after the add callback, and custom third-party unsaved runtime fields remain
separate concerns. This is not a guarantee that every condition-specific gameplay
side effect is replicated. Existing malformed live conditions reconcile on the
next event for their family; rejoin restores native host save state.

V20 on both peers. Test slow on an enemy: same icon/tooltip/speed and duration.
Repeat with haste/stat buffs, two different stat buffs together, removal of only
one, stacking poison, refresh of a weaker/stronger effect, expiry, sleep/paralysis,
weapon buffs, and transformation appearance/removal. Check client self and host
self as well as enemies, and rejoin with effects active. Look for
`Condition state reconciled: chara ..., condition ..., instances ...` in client logs.
