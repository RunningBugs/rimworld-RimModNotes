using System;
using DeathWeapon;

var fixture = new Fixture();
var field = fixture.GetFieldOrProperty<int>("number", out var number);
if (field == null || number != 7) throw new Exception("Private field lookup failed");
if (fixture.GetFieldOrProperty<string>("Secret", out var secret) != null || secret != "secret")
    throw new Exception("Property fallback failed");
object[] parameters = { 4, 2 };
if (fixture.CallMethod<int>("Add", ref parameters) != 10 || (int)parameters[1] != 6)
    throw new Exception("Method/ref-parameter invocation failed");
object[] parentArgs = { 1, 3 };
if (new Derived().CallMethod<int>("Add", ref parentArgs, typeof(Fixture)) != 5
    || (int)parentArgs[1] != 4) throw new Exception("Explicit declaring-type call failed");
try { fixture.GetFieldOrProperty<int>("missing", out _); throw new Exception("Missing member accepted"); }
catch (Exception ex) when (ex.Message.Contains("not found")) { }
Console.WriteLine("PASS: private field/property, method/ref args, explicit declaring type, missing member");

class Fixture
{
    private int number = 7;
    private string Secret => number == 7 ? "secret" : "changed";
    private int Add(int x, ref int y) { y += x; return x + y; }
}
class Derived : Fixture { }
