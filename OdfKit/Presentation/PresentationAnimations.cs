using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Presentation
{
    public enum OdfAnimationNodeType
    {
        Sequence,
        Parallel,
        Effect
    }

    public class OdfAnimationNode
    {
        public OdfNode Node { get; }

        public OdfAnimationNodeType Type
        {
            get
            {
                string name = Node.LocalName;
                if (name == "seq") return OdfAnimationNodeType.Sequence;
                if (name == "par") return OdfAnimationNodeType.Parallel;
                return OdfAnimationNodeType.Effect;
            }
        }

        public string? Begin
        {
            get => Node.GetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0");
            set
            {
                if (value == null)
                    Node.RemoveAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0");
                else
                    Node.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", value, "smil");
            }
        }

        public string? Dur
        {
            get => Node.GetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0");
            set
            {
                if (value == null)
                    Node.RemoveAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0");
                else
                    Node.SetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", value, "smil");
            }
        }

        public string? TargetElement
        {
            get => Node.GetAttribute("targetElement", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0");
            set
            {
                if (value == null)
                    Node.RemoveAttribute("targetElement", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0");
                else
                    Node.SetAttribute("targetElement", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", value, "smil");
            }
        }

        public IReadOnlyList<OdfAnimationNode> Children
        {
            get
            {
                var list = new List<OdfAnimationNode>();
                foreach (var child in Node.Children)
                {
                    if (child.NodeType == OdfNodeType.Element && child.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:animation:1.0")
                    {
                        list.Add(new OdfAnimationNode(child));
                    }
                }
                return list.AsReadOnly();
            }
        }

        public OdfAnimationNode(OdfNode node)
        {
            Node = node;
        }

        public OdfAnimationNode AddSequence(string? begin = null)
        {
            var seq = new OdfNode(OdfNodeType.Element, "seq", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
            if (begin != null)
            {
                seq.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", begin, "smil");
            }
            Node.AppendChild(seq);
            return new OdfAnimationNode(seq);
        }

        public OdfAnimationNode AddParallel(string? begin = null)
        {
            var par = new OdfNode(OdfNodeType.Element, "par", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
            if (begin != null)
            {
                par.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", begin, "smil");
            }
            Node.AppendChild(par);
            return new OdfAnimationNode(par);
        }

        public OdfAnimationNode AddEffect(OdfAnimationType effectType, string targetElementId, OdfLength duration, OdfLength delay)
        {
            var filter = new OdfNode(OdfNodeType.Element, "transitionFilter", "urn:oasis:names:tc:opendocument:xmlns:animation:1.0", "anim");
            filter.SetAttribute("targetElement", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", targetElementId, "smil");

            string durStr = $"{duration.ToPoints() / 72.0:F2}s";
            string delayStr = $"{delay.ToPoints() / 72.0:F2}s";

            string typeStr = "fade";
            string? subtypeStr = null;
            string modeStr = "in";

            switch (effectType)
            {
                case OdfAnimationType.FadeIn:
                    typeStr = "fade";
                    modeStr = "in";
                    break;
                case OdfAnimationType.FadeOut:
                    typeStr = "fade";
                    modeStr = "out";
                    break;
                case OdfAnimationType.ZoomIn:
                    typeStr = "zoom";
                    modeStr = "in";
                    break;
                case OdfAnimationType.WipeRight:
                    typeStr = "wipe";
                    subtypeStr = "leftToRight";
                    modeStr = "in";
                    break;
            }

            filter.SetAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", typeStr, "smil");
            if (subtypeStr != null)
            {
                filter.SetAttribute("subtype", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", subtypeStr, "smil");
            }
            filter.SetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", durStr, "smil");
            filter.SetAttribute("begin", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", delayStr, "smil");
            filter.SetAttribute("mode", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0", modeStr, "smil");

            Node.AppendChild(filter);
            return new OdfAnimationNode(filter);
        }
    }
}
