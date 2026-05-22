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

    extension(IElement element)
    {
        public IElement? SelfOrNextElementSibling(Predicate<IElement> predicate)
        {
            if (predicate(element))
            {
                return element;
            }

            while (element.NextElementSibling is IElement next)
            {
                if (predicate(next))
                {
                    return next;
                }

                element = next;
            }

            return null;
        }
    }
}
