namespace BugfixesAndQoL
{
    internal static class MoveFormationSpacingPolicy
    {
        public const int Minimum = 1;
        public const int Maximum = 4;
        public const int Default = 2;

        public static int Normalize(int value) =>
            value < Minimum || value > Maximum ? Default : value;
    }
}
