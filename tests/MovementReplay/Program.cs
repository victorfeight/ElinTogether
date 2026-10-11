using ElinTogether.Patches;
using UnityEngine;
// Synthetic controlled settings, not a claim about the live Unity asset values.
foreach(var combat in new[]{false,true})foreach(var burst in new[]{false,true}) {
 ActionModeCombat.Activated=combat;RenderObject.animeSetting=new();var c=new Chara();var p=new RenderParam();
 void Move(int x,int z){var from=c.pos.Copy();c.pos=new(x,z);CombatPresentation.Record(c,.3f,1);CombatPresentation.SnapCorrection(c,from);}
 void Draw(){RenderObject.gameSpeed=1;RenderObject.gameDelta=Core.delta;var dest=c.pos.PositionCenter();CombatPresentation.Before(c.renderer,out var state);c.renderer.UpdatePosition(ref dest,p);CombatPresentation.After(c.renderer,state);}
 Move(1,0);if(!burst)for(int i=0;i<30;i++)Draw();Move(1,1);
 bool cut=false;for(int i=0;i<30;i++){Draw();var v=c.renderer.position;if(v.x<.99f&&v.y>.01f)cut=true;}
 Console.WriteLine($"combat={combat} two_steps_before_draw={burst} cuts_corner={cut}");
 if(cut!=burst)throw new Exception("Unexpected replay result");
}
Console.WriteLine("PASS: same ordered steps, no duplicates; burst corner cutting occurs in BOTH modes under equal clocks.");
// Independently vary packet spacing and live turbo. This demonstrates the exact
// combat-only clock branch without pretending these are captured session values.
bool Replay(bool combat,int gap,float liveTurbo,bool settle) {
 ActionModeCombat.Activated=combat;RenderObject.animeSetting=new();var c=new Chara();var p=new RenderParam();
 void Move(int x,int z){var from=c.pos.Copy();c.pos=new(x,z);if(settle)CombatPresentation.RecordReceivedMove(c,from,.3f,1);else {CombatPresentation.Record(c,.3f,1);CombatPresentation.SnapCorrection(c,from);}}
 void Draw(){RenderObject.gameSpeed=liveTurbo;RenderObject.gameDelta=Core.delta*liveTurbo;var dest=c.pos.PositionCenter();CombatPresentation.Before(c.renderer,out var state);c.renderer.UpdatePosition(ref dest,p);CombatPresentation.After(c.renderer,state);}
 Move(1,0);for(int i=0;i<gap;i++)Draw();Move(1,1);bool cut=false;
 for(int i=0;i<60;i++){Draw();var v=c.renderer.position;if(v.x<.99f&&v.y>.01f)cut=true;}return cut;
}
int cases=0,combatOnly=0;
foreach(int gap in new[]{0,1,3,6,10,15,20,30})foreach(float liveTurbo in new[]{1f,2f,4f}) {
 var normal=Replay(false,gap,liveTurbo,false);var combat=Replay(true,gap,liveTurbo,false);
 if(combat&&!normal){combatOnly++;Console.WriteLine($"DIFFERENTIAL gap_frames={gap} live_speed={liveTurbo} recorded_speed=1 normal_cut={normal} combat_cut={combat}");}
 foreach(bool active in new[]{false,true}){if(Replay(active,gap,liveTurbo,true))throw new Exception("Settled replay still cut corner");cases++;}
}
if(combatOnly==0)throw new Exception("No differential reproduction found");
Console.WriteLine($"PASS: {combatOnly} combat-only differential cases; {cases} production corner-boundary cases passed. Production RecordReceivedMove tested.");
// Legal unit-step L route around a solid tile centered at (1,1).
// All four distinct moves arrive before rendering; no logical move crosses it.
foreach(bool settle in new[]{false,true}) {
 ActionModeCombat.Activated=true;RenderObject.animeSetting=new();var c=new Chara();var p=new RenderParam();
 foreach(var target in new[]{new Point(1,0),new Point(2,0),new Point(2,1),new Point(2,2)}){

  var from=c.pos.Copy();c.pos=target;if(settle)CombatPresentation.RecordReceivedMove(c,from,.3f,1);else {CombatPresentation.Record(c,.3f,1);CombatPresentation.SnapCorrection(c,from);}
 }
 bool insideWall=false;
 for(int i=0;i<60;i++){
  RenderObject.gameSpeed=1;RenderObject.gameDelta=Core.delta;var dest=c.pos.PositionCenter();CombatPresentation.Before(c.renderer,out var state);c.renderer.UpdatePosition(ref dest,p);CombatPresentation.After(c.renderer,state);
  var v=c.renderer.position;if(v.x>.5f&&v.x<1.5f&&v.y>.5f&&v.y<1.5f)insideWall=true;
 }
 Console.WriteLine($"SOLID WALL four_ordered_unit_steps settle={settle} sprite_inside_wall={insideWall}");
 if(insideWall==settle)throw new Exception("Solid-wall replay failed");
}
// No-op confirmations must neither finish a partial step nor replace its pace.
{
 ActionModeCombat.Activated=true;var c=new Chara();c.pos=new(1,0);c.renderer.movePoint=new(1,0);c.renderer.position=new(.25f,0,0);c.renderer.isMoving=true;
 CombatPresentation.Record(c,.3f,2);
 CombatPresentation.RecordReceivedMove(c,c.pos.Copy(),.9f,1);
 if(c.renderer.position.x!=.25f||!c.renderer.IsMoving)throw new Exception("No-op disturbed animation");
 CombatPresentation.Before(c.renderer,out var state);if(RenderObject.gameSpeed!=2)throw new Exception("No-op replaced pace");CombatPresentation.After(c.renderer,state);
 c.renderer.first=true;c.pos=new(2,0);CombatPresentation.RecordReceivedMove(c,new(1,0),.3f,1);
 if(!c.renderer.first)throw new Exception("First draw reset was lost");
 c.renderer.first=false;c.pos=new(9,0);CombatPresentation.RecordReceivedMove(c,new(2,0),.3f,1);
 if(!c.renderer.first||c.renderer.position.x!=9)throw new Exception("Large correction failed to snap");
 Console.WriteLine("PASS: no-op preserves partial animation and pace; initial draw and large correction retain native snap.");
}
