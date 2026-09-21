# TpHugeShippingContainer stable compatibility rebuild

Rebuild of TP@黒猫亭's `TpHugeShippingContainer` for Elin stable `23.338.2`.

Current Elin copies the placed shipping chest's dimensions into the shared shipping
container whenever it opens. Old 8x5 placed chests therefore undo the original mod's
16x10 initialization. This rebuild resizes the placed shipping chest before that copy,
and continues to update the source row and shared container.
