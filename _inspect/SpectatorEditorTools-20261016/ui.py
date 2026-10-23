from pathlib import Path
import xml.etree.ElementTree as ET
import copy

root=Path(__file__).resolve().parents[2]
mod=root/'Testmods/SpectatorEditorBuildTest'
ns={'p':'http://schemas.microsoft.com/winfx/2006/xaml/presentation','x':'http://schemas.microsoft.com/winfx/2006/xaml','local':'clr-namespace:CrusaderDE'}
vanilla=ET.parse(root/'_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/resources/xaml/Assets/GUI/XAMLResources/HUD_AlliesPanel.xaml')
goods=[]
for button in vanilla.iter('{'+ns['p']+'}Button'):
    param=button.get('CommandParameter','')
    if len(param)==3 and param.startswith('G') and param[1:].isdigit():
        goods.append((param,button))
assert len(goods)==21, len(goods)
ET.register_namespace('',ns['p']); ET.register_namespace('x',ns['x']); ET.register_namespace('local',ns['local'])
buttons=[]
for index,(param,button) in enumerate(goods):
    b=copy.deepcopy(button)
    for attr in ['Margin','HorizontalAlignment','VerticalAlignment']: b.attrib.pop(attr,None)
    b.set('Grid.Row',str(index//11)); b.set('Grid.Column',str(index%11))
    b.set('Margin','4,10'); b.set('Command','{Binding ActionCommand}')
    b.set('ToolTip','{Binding Text[Good'+param[1:]+']}'); b.set('ToolTipService.ShowDuration','60000')
    buttons.append(ET.tostring(b,encoding='unicode'))

def command(label,param,width='145'):
    if param in ('Attack','Defend','Cancel'):
        oldparam='Attack0' if param=='Attack' else param
        original=next(b for b in vanilla.iter('{'+ns['p']+'}Button') if b.get('CommandParameter')==oldparam)
        sprites=' '.join('local:'+key.split('}',1)[1]+'="'+value+'"' for key,value in original.attrib.items() if key.startswith('{'+ns['local']+'}PropEx.Sprite'))
        return f'<Button Width="{width}" Height="45" Margin="3" Style="{{StaticResource ToolsButton}}" Command="{{Binding ActionCommand}}" CommandParameter="{param}" ToolTip="{{Binding Text[{label}Help]}}" ToolTipService.ShowDuration="60000"><StackPanel Orientation="Horizontal"><Image Width="32" Height="32" Source="'+original.get('{'+ns['local']+'}PropEx.Sprite1','{StaticResource UISprites E001}')+f'"/><TextBlock Text="{{Binding Text[{label}]}}" FontSize="14" Foreground="#FFEFF3C6" VerticalAlignment="Center" Margin="6,0"/></StackPanel></Button>'
    return f'<Button Width="{width}" Height="30" Margin="3" Style="{{StaticResource ToolsButton}}" Content="{{Binding Text[{label}]}}" Command="{{Binding ActionCommand}}" CommandParameter="{param}" ToolTip="{{Binding Text[{label}Help]}}" ToolTipService.ShowDuration="60000"/>'
def picker(prefix):
    rows=[]
    for owner in range(1,9):
        if prefix=='Owner':
            name=f'SpectatorEditorHumanOwner{owner}'; number=f'SpectatorEditorHumanNumber{owner}'; cmd=''
        else:
            name=f'TestTarget{owner}'; number=f'TestTargetText{owner}'; cmd=f'Command="{{Binding ActionCommand}}" CommandParameter="Target{owner}"'
        rows.append(f'<Button x:Name="{name}" Width="150" Height="54" Margin="2" Style="{{StaticResource ToolsButton}}" {cmd} ToolTip="{{Binding Text[SelectHelp]}}" ToolTipService.ShowDuration="60000"><StackPanel Orientation="Horizontal"><Image x:Name="{prefix}Face{owner}" Width="34" Height="34"/><TextBlock x:Name="{number}" FontSize="13" Width="108" TextWrapping="Wrap" VerticalAlignment="Center" Foreground="White"/></StackPanel></Button>')
    return ''.join(rows)

xaml='''<?xml version="1.0" encoding="utf-8"?>
<Patch xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:local="clr-namespace:CrusaderDE" xmlns:seui="clr-namespace:SHCDESE.UI;assembly=SHCDESE">
  <Operation Type="Add" XPath="/n:UserControl/n:Grid">
    <Content>
      <Canvas x:Name="SpectatorEditorHumanCanvas" Grid.Row="0" Grid.RowSpan="4" Grid.Column="0" Grid.ColumnSpan="4" HorizontalAlignment="Stretch" VerticalAlignment="Stretch" Panel.ZIndex="10031">
        <Canvas.Resources>
          <Style x:Key="ToolsTooltip" TargetType="{x:Type ToolTip}">
            <Setter Property="Background" Value="#FF1D1710"/><Setter Property="Foreground" Value="White"/>
            <Setter Property="BorderBrush" Value="#FFF2D48A"/><Setter Property="seui:ToolTipResolutionScale.Enabled" Value="True"/>
            <Setter Property="FontSize" Value="20"/><Setter Property="MaxWidth" Value="680"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="{x:Type ToolTip}"><Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" Padding="12" MaxWidth="{TemplateBinding MaxWidth}"><TextBlock Text="{TemplateBinding Content}" Foreground="{TemplateBinding Foreground}" FontSize="{TemplateBinding FontSize}" TextWrapping="Wrap"/></Border></ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="{x:Type ToolTip}" BasedOn="{StaticResource ToolsTooltip}"/>
          <Style x:Key="ToolsButton" TargetType="{x:Type Button}">
            <Setter Property="Background" Value="#E0261710"/><Setter Property="Foreground" Value="#FFEFF3C6"/>
            <Setter Property="BorderBrush" Value="#FFE5C884"/><Setter Property="BorderThickness" Value="1"/>
            <Setter Property="FontSize" Value="14"/><Setter Property="Focusable" Value="False"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="{x:Type Button}"><Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="#FF714221"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.4"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
          </Style>
        </Canvas.Resources>
        <Border x:Name="SpectatorEditorHumanPanel" Width="700" Height="570" Visibility="Collapsed" Background="#F018100B" BorderBrush="#FFE5C884" BorderThickness="1">
          <Viewbox Stretch="Uniform" StretchDirection="DownOnly"><Grid x:Name="TestToolsRoot" Width="698" Height="568">
            <Grid.RowDefinitions><RowDefinition Height="38"/><RowDefinition Height="40"/><RowDefinition Height="100"/><RowDefinition Height="50"/><RowDefinition Height="340"/></Grid.RowDefinitions>
            <Border x:Name="SpectatorEditorHumanDrag" Grid.Row="0" Background="#AA6F1712" Cursor="SizeAll" ToolTip="{Binding Text[DragHelp]}" ToolTipService.ShowDuration="60000"><TextBlock Text="{Binding Text[Title]}" FontSize="18" Foreground="#FFEFF3C6" Margin="12,6"/></Border>
            <Button Grid.Row="0" HorizontalAlignment="Right" Width="35" Height="30" Margin="4" Style="{StaticResource ToolsButton}" Content="−/+" Command="{Binding ActionCommand}" CommandParameter="Collapse" ToolTip="{Binding Text[CollapseHelp]}" ToolTipService.ShowDuration="60000"/>
            <StackPanel Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Center" Visibility="{Binding ExpandedVisibility}">
              <Button x:Name="SpectatorEditorHumanToggle" Width="185" Height="30" Margin="3" Style="{StaticResource ToolsButton}" ToolTip="{Binding Text[PlacementHelp]}" ToolTipService.ShowDuration="60000"><TextBlock x:Name="SpectatorEditorHumanToggleText" FontSize="14" Foreground="White"/></Button>
''' + command('Goods','Goods','185') + command('Orders','Orders','185') + '''
            </StackPanel>
            <Grid Grid.Row="2" Visibility="{Binding ExpandedVisibility}"><Grid.RowDefinitions><RowDefinition Height="32"/><RowDefinition Height="68"/></Grid.RowDefinitions>
              <StackPanel Orientation="Horizontal" HorizontalAlignment="Center"><TextBlock Text="{Binding SelectedPlayer}" Foreground="#FFEFF3C6" FontSize="15" VerticalAlignment="Center" Width="470"/>
''' + command('Previous','PrevOwners','30') + '''<TextBlock Text="{Binding OwnerPageLabel}" Foreground="White" VerticalAlignment="Center"/>''' + command('Next','NextOwners','30') + '''</StackPanel>
              <StackPanel x:Name="SpectatorEditorHumanOwners" Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Center">''' + picker('Owner') + '''</StackPanel>
            </Grid>
            <TextBlock Grid.Row="3" Text="{Binding Status}" Foreground="#FFF2D48A" FontSize="14" TextWrapping="Wrap" Margin="16,4" Visibility="{Binding ExpandedVisibility}"/>
            <Grid Grid.Row="4" Visibility="{Binding PlacementVisibility}"><StackPanel Margin="30,20"><TextBlock Text="{Binding Text[PlacementTitle]}" Foreground="#FFEFF3C6" FontSize="22"/><TextBlock Text="{Binding Text[PlacementHelp]}" Foreground="White" FontSize="16" TextWrapping="Wrap" Margin="0,18"/><TextBlock Text="{Binding Text[EditorNotice]}" Foreground="#FFC2AB82" FontSize="14" TextWrapping="Wrap"/></StackPanel></Grid>
            <Grid Grid.Row="4" Visibility="{Binding GoodsVisibility}"><Grid.RowDefinitions><RowDefinition Height="125"/><RowDefinition Height="45"/><RowDefinition Height="45"/><RowDefinition Height="45"/><RowDefinition Height="80"/></Grid.RowDefinitions>
              <!-- Exact Vanilla send-goods sprite choices; separate DataContext and command. -->
              <Grid Width="660" HorizontalAlignment="Center"><Grid.RowDefinitions><RowDefinition/><RowDefinition/></Grid.RowDefinitions><Grid.ColumnDefinitions>''' + '<ColumnDefinition/>'*11 + '''</Grid.ColumnDefinitions>''' + ''.join(buttons) + '''</Grid>
              <StackPanel Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Center"><TextBlock Text="{Binding SelectedGood}" Foreground="#FFEFF3C6" FontSize="18" VerticalAlignment="Center" Width="240"/><TextBox Width="220" FontSize="18" Text="{Binding Amount, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" ToolTip="{Binding Text[AmountHelp]}" ToolTipService.ShowDuration="60000"/></StackPanel>
              <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Center">''' + ''.join(command('Plus'+str(n),'X'+str(n),'100') for n in (5,10,25,100,500)) + '''</StackPanel>
              <StackPanel Grid.Row="3" Orientation="Horizontal" HorizontalAlignment="Center">''' + ''.join(command('Minus'+str(n),'X'+str(n)+'-','100') for n in (5,10,25,100,500)) + '''</StackPanel>
              <StackPanel Grid.Row="4" HorizontalAlignment="Center">''' + command('Credit','Credit','300') + '''<TextBlock Text="{Binding Text[ReserveHelp]}" Foreground="#FFC2AB82" FontSize="13" Width="630" TextWrapping="Wrap"/></StackPanel>
            </Grid>
            <Grid Grid.Row="4" Visibility="{Binding OrdersVisibility}"><Grid.RowDefinitions><RowDefinition Height="32"/><RowDefinition Height="62"/><RowDefinition Height="55"/><RowDefinition Height="60"/><RowDefinition Height="70"/></Grid.RowDefinitions>
              <StackPanel Orientation="Horizontal" HorizontalAlignment="Center"><TextBlock Text="{Binding SelectedTarget}" Foreground="#FFEFF3C6" FontSize="15" Width="470" VerticalAlignment="Center"/>''' + command('Previous','PrevTargets','30') + '''<TextBlock Text="{Binding TargetPageLabel}" Foreground="White" VerticalAlignment="Center"/>''' + command('Next','NextTargets','30') + '''</StackPanel>
              <StackPanel Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Center">''' + picker('Target') + '''</StackPanel>
              <CheckBox Grid.Row="2" Content="{Binding Text[Force]}" IsChecked="{Binding ForceAcceptance, Mode=TwoWay}" Foreground="White" FontSize="17" Margin="28,10" ToolTip="{Binding Text[ForceHelp]}" ToolTipService.ShowDuration="60000"/>
              <StackPanel Grid.Row="3" Orientation="Horizontal" HorizontalAlignment="Center">''' + command('Attack','Attack','195') + command('Defend','Defend','195') + command('Cancel','Cancel','195') + '''</StackPanel>
              <TextBlock Grid.Row="4" Text="{Binding Text[OrdersHelp]}" Foreground="#FFC2AB82" FontSize="14" TextWrapping="Wrap" Margin="28,8"/>
            </Grid>
          </Grid></Viewbox>
        </Border>
      </Canvas>
    </Content>
  </Operation>
</Patch>
'''
# ElementTree adds duplicate namespace declarations per copied button; inherited declarations suffice.
for name,uri in ns.items(): xaml=xaml.replace(f' xmlns:{name}="{uri}"','') if name=='p' else xaml
xaml=xaml.replace(' xmlns="'+ns['p']+'"','').replace(' xmlns:local="'+ns['local']+'"','').replace(' xmlns:x="'+ns['x']+'"','')
xaml=xaml.replace('Source="{Binding AlliesFace0}"', 'Source="{Binding SelectedTargetFace}"')
xaml=xaml.replace('<Patch ', '<Patch xmlns:x="'+ns['x']+'" xmlns:local="'+ns['local']+'" ',1)
ET.fromstring(xaml)
(mod/'Patches/Assets/GUI/XAML/IngameUIScreens.xaml').write_text(xaml,encoding='utf-8',newline='\r\n')
