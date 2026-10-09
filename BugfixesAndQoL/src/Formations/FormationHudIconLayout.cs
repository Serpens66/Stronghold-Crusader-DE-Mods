namespace BugfixesAndQoL.UnitCommands
{
    internal static class FormationHudIconLayout
    {
        internal const float PointSize = 2.8f;
        internal const float PointScale = 0.65f;
        internal const float Left = 7f;
        internal const float Top = 5.5f;
        internal const float Stroke = 0.55f;
        internal const string InnerShieldPath = "M 5,4 L 30,4 L 29,18 Q 27,25 17.5,30 Q 8,25 6,18 Z";
        internal static int[][] Points(FormationKind kind)
        {
            switch (kind)
            {
                case FormationKind.Vanilla: return new[] { P(14,3),P(8,9),P(14,9),P(20,9),P(4,15),P(10,15),P(16,15),P(22,15),P(8,21),P(14,21),P(20,21),P(14,27) };
                case FormationKind.Line: return new[] { P(2,14),P(8,14),P(14,14),P(20,14),P(26,14) };
                case FormationKind.Column: return new[] { P(14,2),P(14,8),P(14,14),P(14,20),P(14,26) };
                case FormationKind.Wedge: return new[] { P(14,4),P(8,12),P(14,12),P(20,12),P(2,20),P(8,20),P(14,20),P(20,20),P(26,20) };
                case FormationKind.Circle: return new[] { P(14,3),P(7,7),P(21,7),P(3,14),P(25,14),P(7,21),P(21,21),P(14,25) };
                default: return new[] { P(7,7),P(14,7),P(21,7),P(7,14),P(14,14),P(21,14),P(7,21),P(14,21),P(21,21) };
            }
        }
        private static int[] P(int x, int y) => new[] { x, y };
    }
}
