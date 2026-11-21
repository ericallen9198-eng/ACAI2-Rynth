-- Explicit Start replaces the different macro left by the identity regression test.
sim.ignoreLoad=false
step(25);click('Resume run');step(40)
assert(ai.MacroRunning and sim.meta=='Marae Lassel - Embedded','allocation test needs running mission')
sim.delta=.005
local before=sim.exprCalls
for i=1,1000 do
 step()
 if i%2==0 then sim.chat({Message='You graze Tower Guardian for 85 points of bludgeoning damage!'}) end
 if i%40==0 then sim.render() end
end
assert(sim.exprCalls-before<150,'high tick rate must preserve five-second expression budget: '..(sim.exprCalls-before))
sim.delta=nil
