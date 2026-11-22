using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace GatTelemetry;
internal static class NeonOverlaySizing
{
    internal const double Aspect=1200.0/380.0;
    internal static int[] Constrain(int[] r,int edge,double currentWidth,double currentHeight,double minWidth,double minHeight)
    {
        double width=Math.Max(1,r[2]-r[0]),height=Math.Max(1,r[3]-r[1]);
        bool byWidth=edge==1||edge==2||((edge!=3&&edge!=6)&&Math.Abs(width-currentWidth)/Aspect>=Math.Abs(height-currentHeight));
        double minimum=Math.Max(minWidth,minHeight*Aspect);
        width=Math.Max(minimum,byWidth?width:height*Aspect);
        int w=(int)Math.Round(width),h=(int)Math.Round(width/Aspect);
        int left=r[0],top=r[1],right=r[2],bottom=r[3];
        if(edge==1||edge==4||edge==7)left=right-w;else right=left+w;
        if(edge==3||edge==4||edge==5)top=bottom-h;else bottom=top+h;
        return new[]{left,top,right,bottom};
    }
}
internal sealed partial class MainForm
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NeonNativeRect {public int Left,Top,Right,Bottom;}
    private bool HandleNeonSizing(int message,IntPtr edge,IntPtr pointer)
    {
        if(message!=0x0214||pointer==IntPtr.Zero||_roadLifeOverlay==null)return false;
        var r=(NeonNativeRect)Marshal.PtrToStructure(pointer,typeof(NeonNativeRect));
        double dpi=_roadLifeSource?.CompositionTarget?.TransformToDevice.M11??1;
        var size=NeonOverlaySizing.Constrain(new[]{r.Left,r.Top,r.Right,r.Bottom},edge.ToInt32(),
            _roadLifeOverlay.Width*dpi,_roadLifeOverlay.Height*dpi,_roadLifeOverlay.MinWidth*dpi,_roadLifeOverlay.MinHeight*dpi);
        r.Left=size[0];r.Top=size[1];r.Right=size[2];r.Bottom=size[3];Marshal.StructureToPtr(r,pointer,false);return true;
    }
    private static bool NeonIsResizeControl(DependencyObject source)
    {
        while(source!=null){
            if(source is System.Windows.Controls.Primitives.ResizeGrip||source is System.Windows.Controls.Primitives.Thumb)return true;
            source=source is Visual?VisualTreeHelper.GetParent(source):LogicalTreeHelper.GetParent(source);
        }
        return false;
    }
}
