// Source https://git.sr.ht/~rabbits/uxn/tree/main/item/src/uxnasm.c

using UxnAsm;

if (args.Length != 2)
{
    Console.WriteLine("usage uxnasm input.tal ouput.rom");
    return;
}

var program = new UxnProgram();
program.Compile(args[0], args[1]);
// TODO Test