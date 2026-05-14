using AngleSharp.Dom;

namespace OriginLab.DocumentGeneration;

internal static class DomExtensions
{
    extension(IEnumerable<IChildNode> nodes)
    {
        public void Remove()
        {
            foreach (var node in nodes.ToList())
            {
                node.Remove();
            }
        }
    }
}
