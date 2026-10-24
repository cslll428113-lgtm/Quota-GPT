using System;
namespace QuotaNext;
internal enum DockEdge { None, Left, Right }
internal readonly record struct DockArea(double Left,double Top,double Right,double Bottom);
internal readonly record struct DockBox(double X,double Y,double Width,double Height);
internal static class DockGeometry
{
 internal static DockEdge Detect(DockBox box,DockArea work,double threshold)
 {
  double left=Math.Max(0,box.X-work.Left),right=Math.Max(0,work.Right-box.X-box.Width);
  return Math.Min(left,right)>threshold?DockEdge.None:left<=right?DockEdge.Left:DockEdge.Right;
 }
 internal static DockBox Place(DockBox anchor,double width,double height,DockArea work,DockEdge edge)
 {
  double x=edge==DockEdge.Left?work.Left:edge==DockEdge.Right?work.Right-width:anchor.X;
  return new(Math.Clamp(x,work.Left,Math.Max(work.Left,work.Right-width)),
   Math.Clamp(anchor.Y,work.Top,Math.Max(work.Top,work.Bottom-height)),width,height);
 }
}
