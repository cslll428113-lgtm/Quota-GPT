from pathlib import Path
import xml.etree.ElementTree as E
base=Path(__file__).resolve().parent.parent
out=base/'direct-build'
ns='http://schemas.microsoft.com/winfx/2006/xaml/presentation';xn='http://schemas.microsoft.com/winfx/2006/xaml'
E.register_namespace('',ns);E.register_namespace('x',xn)
window=E.parse(base/'MainWindow.xaml').getroot();app=E.parse(base/'App.xaml').getroot()
resources=E.Element('{'+ns+'}ResourceDictionary')
for child in app.find('{'+ns+'}Application.Resources'):resources.append(child)
(out/'Resources.xaml').write_bytes(E.tostring(resources,encoding='utf-8'))
root=window.find('{'+ns+'}Grid')
# Bind event handlers explicitly when loading the same XAML without BAML/MSBuild.
events={'Click','Closed','SizeChanged','MouseRightButtonUp'}
fields=[];wires=[]
for el in root.iter():
 name=el.get('{'+xn+'}Name')
 if not name and any(k in events for k in el.attrib):
  name='DirectNode'+str(len(fields));el.set('{'+xn+'}Name',name)
 if name:fields.append((el.tag.split('}')[-1],name))
 for key in list(el.attrib):
  if key in events:
   assert name,(key,el.tag)
   wires.append(f'{name}.{key} += {el.attrib.pop(key)};')
(out/'Content.xaml').write_bytes(E.tostring(root,encoding='utf-8'))
code='''#if DIRECT_BUILD
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Shapes;
using System.Windows.Media;
[assembly: AssemblyVersion("0.6.1.0")]
[assembly: AssemblyFileVersion("0.6.1.0")]
namespace QuotaNext;
internal static class DirectEntry {
 [STAThread] static void Main(){var app=new App();using var r=Assembly.GetExecutingAssembly().GetManifestResourceStream("QuotaNext.Resources.xaml")!;app.Resources=(ResourceDictionary)XamlReader.Load(r);app.Run();}
}
public partial class MainWindow {
'''
for typ,name in fields:code+=f' internal {typ} {name}=null!;\n'
code+=''' void InitializeComponent(){
 Width=272; SizeToContent=SizeToContent.Height; ResizeMode=ResizeMode.NoResize; WindowStyle=WindowStyle.None; AllowsTransparency=true; Background=Brushes.Transparent; Topmost=true; ShowInTaskbar=false; FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI");UseLayoutRounding=true;Foreground=new SolidColorBrush(Color.FromRgb(236,240,245));Title="Quota · GPT 额度";
 using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("QuotaNext.Content.xaml")!;
 var root=(Grid)XamlReader.Load(s);
'''
for typ,name in fields:code+=f' {name}=({typ})root.FindName("{name}");\n'
for key in ['PreviewMouseLeftButtonDown','PreviewMouseMove','PreviewMouseLeftButtonUp','MouseEnter','MouseLeave']:
 code+=f' {key} += {window.get(key)};\n'
code+='\n'.join(wires)+'\n Content=root;\n }\n}\n#endif\n'
(out/'DirectBuild.cs').write_text(code)
print('Generated named controls:',len(fields),'event bindings:',len(wires)+5)
