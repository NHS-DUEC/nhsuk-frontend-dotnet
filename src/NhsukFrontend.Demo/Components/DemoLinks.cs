namespace NhsukFrontend.Demo.Components;

public static class DemoLinks
{
    public static string Component(string name) => $"/components/{name}";
    public static string Preview(string name, int index) => $"/preview/{name}/{index}";
}
