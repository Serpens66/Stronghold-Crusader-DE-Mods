using BugfixesAndQoL.UnitCommands;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.Text.RegularExpressions;

[TestClass]
public class FormationHudIconTests
{
    [TestMethod]
    public void EveryFormationDotIncludingItsStrokeFitsInsideTheActualInnerShield()
    {
        var polygon = FlattenShield(FormationHudIconLayout.InnerShieldPath);
        foreach (FormationKind kind in Enum.GetValues<FormationKind>())
        {
            var points = FormationHudIconLayout.Points(kind);
            Assert.IsTrue(points.Length > 0, kind.ToString());
            foreach (var point in points)
            {
                double x = FormationHudIconLayout.Left + point[0] * FormationHudIconLayout.PointScale + FormationHudIconLayout.PointSize / 2;
                double y = FormationHudIconLayout.Top + point[1] * FormationHudIconLayout.PointScale + FormationHudIconLayout.PointSize / 2;
                double radius = (FormationHudIconLayout.PointSize + FormationHudIconLayout.Stroke) / 2;
                for (int angle = 0; angle < 360; angle += 5)
                {
                    double radians = angle * Math.PI / 180;
                    Assert.IsTrue(Contains(polygon, (x + radius * Math.Cos(radians), y + radius * Math.Sin(radians))),
                        $"{kind}: dot ({point[0]}, {point[1]}) crosses the shield at {angle} degrees.");
                }
            }
        }
    }

    [TestMethod]
    public void RedPlayerUsesSaturatedRedWithAThinOutline()
    {
        Assert.AreEqual((byte)255, FormationHudIconLayout.ColourChannel(1, 0));
        Assert.AreEqual((byte)48, FormationHudIconLayout.ColourChannel(1, 1));
        Assert.AreEqual((byte)40, FormationHudIconLayout.ColourChannel(1, 2));
    }
    private static List<(double x, double y)> FlattenShield(string path)
    {
        var tokens = Regex.Matches(path, "[MLQZ]|[0-9.]+") .Select(m => m.Value).ToArray();
        var polygon = new List<(double x, double y)>();
        int index = 0;
        double Number() => double.Parse(tokens[index++], CultureInfo.InvariantCulture);
        while (index < tokens.Length)
        {
            string command = tokens[index++];
            if (command == "M" || command == "L") polygon.Add((Number(), Number()));
            else if (command == "Q")
            {
                var start = polygon.Last(); var control = (x: Number(), y: Number()); var end = (x: Number(), y: Number());
                for (int step = 1; step <= 200; step++)
                {
                    double t = step / 200.0, u = 1 - t;
                    polygon.Add((u * u * start.x + 2 * u * t * control.x + t * t * end.x,
                        u * u * start.y + 2 * u * t * control.y + t * t * end.y));
                }
            }
            else if (command != "Z") Assert.Fail("Unsupported shield path command " + command);
        }
        return polygon;
    }

    private static bool Contains(List<(double x, double y)> polygon, (double x, double y) point)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i]; var b = polygon[j];
            if ((a.y > point.y) != (b.y > point.y) &&
                point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }
}
