from pathlib import Path
import xml.etree.ElementTree as ET
import copy
root=Path(__file__).resolve().parents[2]
path=root/'Testmods/SpectatorEditorBuildTest/Patches/Assets/GUI/XAML/IngameUIScreens.xaml'
old=ET.parse(path)
goods=[copy.deepcopy(b) for b in old.iter('Button') if b.get('CommandParameter','').startswith('G') and b.get('CommandParameter','')[1:].isdigit()]
assert len(goods)==21
nsx='http://schemas.microsoft.com/winfx/2006/xaml'
nslocal='clr-namespace:CrusaderDE'
ET.register_namespace('x',nsx);ET.register_namespace('local',nslocal)
tiles=[]
for b in goods:
    key=b.get('CommandParameter')
    for attr in ['Grid.Row','Grid.Column','Margin','HorizontalAlignment','VerticalAlignment']:b.attrib.pop(attr,None)
    b.set('Margin','0')
    button=ET.tostring(b,encoding='unicode').replace(f' xmlns:local="{nslocal}"','')
    tiles.append(f'''<Border Width="50" Height="40" Margin="2,1"><Border.Style><Style TargetType="{{x:Type Border}}"><Setter Property="BorderThickness" Value="2"/><Setter Property="BorderBrush" Value="Transparent"/><Style.Triggers><DataTrigger Binding="{{Binding SelectedGoodKey}}" Value="{key}"><Setter Property="BorderBrush" Value="#FFF2D48A"/><Setter Property="Background" Value="#FF714221"/></DataTrigger></Style.Triggers></Style></Border.Style>{button}</Border>''')
def button(label,command,width=190,height=28):
    return f'<Button Width="{width}" Height="{height}" Margin="3" Style="{{StaticResource ToolsButton}}" Content="{{Binding Text[{label}]}}" Command="{{Binding ActionCommand}}" CommandParameter="{command}" ToolTip="{{Binding Text[{label}Help]}}" ToolTipService.ShowDuration="60000"/>'
def tab(label,command):
    return f'''<Button Width="195" Height="28" Margin="3,0" Content="{{Binding Text[{label}]}}" Command="{{Binding ActionCommand}}" CommandParameter="{command}" ToolTip="{{Binding Text[{label}Help]}}" ToolTipService.ShowDuration="60000"><Button.Style><Style TargetType="{{x:Type Button}}" BasedOn="{{StaticResource ToolsButton}}"><Style.Triggers><DataTrigger Binding="{{Binding ActiveViewKey}}" Value="{command}"><Setter Property="Background" Value="#FF875020"/><Setter Property="BorderBrush" Value="#FFFFE6A8"/></DataTrigger></Style.Triggers></Style></Button.Style></Button>'''
def cards(prefix):
    result=[]
    for owner in range(1,9):
        name=f'SpectatorEditorHumanOwner{owner}' if prefix=='Owner' else f'TestTarget{owner}'
        text=f'SpectatorEditorHumanNumber{owner}' if prefix=='Owner' else f'TestTargetText{owner}'
        command='' if prefix=='Owner' else f'Command="{{Binding ActionCommand}}" CommandParameter="Target{owner}"'
        result.append(f'''<Button x:Name="{name}" Width="148" Height="42" Margin="2" Style="{{StaticResource ToolsButton}}" {command} ToolTip="{{Binding Text[SelectHelp]}}" ToolTipService.ShowDuration="60000"><StackPanel Orientation="Horizontal"><Image x:Name="{prefix}Face{owner}" Width="30" Height="30" Margin="3,0"/><TextBlock x:Name="{text}" FontSize="13" Width="104" TextTrimming="CharacterEllipsis" VerticalAlignment="Center" Foreground="White"/></StackPanel></Button>''')
    return ''.join(result)
xaml='''<?xml version="1.0" encoding="utf-8"?>
<Patch xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:local="clr-namespace:CrusaderDE" xmlns:seui="clr-namespace:SHCDESE.UI;assembly=SHCDESE">
  <Operation Type="Add" XPath="/n:UserControl/n:Grid"><Content>
    <Canvas x:Name="SpectatorEditorHumanCanvas" Grid.Row="0" Grid.RowSpan="4" Grid.Column="0" Grid.ColumnSpan="4" HorizontalAlignment="Stretch" VerticalAlignment="Stretch" Panel.ZIndex="10031">
      <Canvas.Resources>
        <Style x:Key="ToolsTooltip" TargetType="{x:Type ToolTip}">
          <Setter Property="Background" Value="#FF1D1710"/><Setter Property="Foreground" Value="White"/><Setter Property="BorderBrush" Value="#FFF2D48A"/>
          <Setter Property="seui:ToolTipResolutionScale.Enabled" Value="True"/><Setter Property="FontSize" Value="20"/><Setter Property="MaxWidth" Value="680"/>
          <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="{x:Type ToolTip}"><Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" Padding="12" MaxWidth="{TemplateBinding MaxWidth}"><TextBlock Text="{TemplateBinding Content}" Foreground="{TemplateBinding Foreground}" FontSize="{TemplateBinding FontSize}" TextWrapping="Wrap"/></Border></ControlTemplate></Setter.Value></Setter>
        </Style>
        <Style TargetType="{x:Type ToolTip}" BasedOn="{StaticResource ToolsTooltip}"/>
        <Style x:Key="ToolsButton" TargetType="{x:Type Button}">
          <Setter Property="Background" Value="#E0261710"/><Setter Property="Foreground" Value="#FFEFF3C6"/><Setter Property="BorderBrush" Value="#FFD6B77C"/><Setter Property="BorderThickness" Value="1"/><Setter Property="FontSize" Value="14"/><Setter Property="Focusable" Value="False"/>
          <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="{x:Type Button}"><Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="#FF714221"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.4"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
        </Style>
      </Canvas.Resources>
      <Border x:Name="SpectatorEditorHumanPanel" Width="240" Background="#F018100B" BorderBrush="#FFE5C884" BorderThickness="1" Visibility="Collapsed">
        <Viewbox Stretch="Uniform" StretchDirection="DownOnly"><Grid x:Name="TestToolsRoot" Width="238">
          <Grid.RowDefinitions><RowDefinition Height="32"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
          <Border x:Name="SpectatorEditorHumanDrag" Background="#AA6F1712" Cursor="SizeAll" ToolTip="{Binding Text[DragHelp]}" ToolTipService.ShowDuration="60000"><TextBlock Text="{Binding Text[Title]}" FontSize="17" Foreground="#FFEFF3C6" Margin="10,5,38,0" TextTrimming="CharacterEllipsis"/></Border>
          <Button HorizontalAlignment="Right" Width="28" Height="26" Margin="3" Style="{StaticResource ToolsButton}" Content="{Binding ExpandLabel}" Command="{Binding ActionCommand}" CommandParameter="Collapse" ToolTip="{Binding Text[CollapseHelp]}" ToolTipService.ShowDuration="60000"/>
          <StackPanel Grid.Row="1" Margin="10,4,10,8" Visibility="{Binding ExpandedVisibility}">
            <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,0,0,6">'''+tab('PlacementTab','Placement')+tab('Goods','Goods')+tab('Orders','Orders')+'''</StackPanel>
            <TextBlock Text="{Binding SelectedPlayer}" Foreground="#FFEFF3C6" FontSize="14" Margin="3,0,3,2" TextTrimming="CharacterEllipsis"/>
            <WrapPanel x:Name="SpectatorEditorHumanOwners" Width="608" ItemWidth="152" ItemHeight="46" Orientation="Horizontal" HorizontalAlignment="Center">'''+cards('Owner')+'''</WrapPanel>
            <StackPanel Visibility="{Binding PlacementVisibility}" Margin="0,4,0,0">
              <Button x:Name="SpectatorEditorHumanToggle" Width="280" Height="30" Margin="3" HorizontalAlignment="Center" Style="{StaticResource ToolsButton}" ToolTip="{Binding Text[PlacementHelp]}" ToolTipService.ShowDuration="60000"><TextBlock x:Name="SpectatorEditorHumanToggleText" FontSize="14" Foreground="White"/></Button>
            </StackPanel>
            <StackPanel Visibility="{Binding GoodsVisibility}" Margin="0,4,0,0">
              <TextBlock Text="{Binding SelectedGood}" Foreground="#FFF2D48A" FontSize="16" HorizontalAlignment="Center" Margin="0,0,0,2"/>
              <WrapPanel Width="594" ItemWidth="54" ItemHeight="42" Orientation="Horizontal" HorizontalAlignment="Center">'''+''.join(tiles)+'''</WrapPanel>
              <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,4,0,0">
                <TextBlock Text="{Binding Text[Amount]}" Foreground="White" FontSize="15" VerticalAlignment="Center" Margin="0,0,10,0"/>
                <TextBox x:Name="TestToolsAmount" Width="145" Height="30" FontSize="18" Text="0" VerticalContentAlignment="Center" ToolTip="{Binding Text[AmountHelp]}" ToolTipService.ShowDuration="60000"/>
                <Button Width="250" Height="30" Margin="10,0,0,0" Style="{StaticResource ToolsButton}" Content="{Binding Text[Credit]}" Command="{Binding ActionCommand}" CommandParameter="Credit" IsEnabled="{Binding CanCredit}" ToolTip="{Binding Text[CreditHelp]}" ToolTipService.ShowDuration="60000"/>
              </StackPanel>
            </StackPanel>
            <StackPanel Visibility="{Binding OrdersVisibility}" Margin="0,4,0,0">
              <TextBlock Text="{Binding SelectedTarget}" Foreground="#FFEFF3C6" FontSize="14" Margin="3,0,3,2" TextTrimming="CharacterEllipsis"/>
              <WrapPanel x:Name="TestToolsTargets" Width="608" ItemWidth="152" ItemHeight="46" Orientation="Horizontal" HorizontalAlignment="Center">'''+cards('Target')+'''</WrapPanel>
              <CheckBox Content="{Binding Text[Force]}" IsChecked="{Binding ForceAcceptance, Mode=TwoWay}" Foreground="White" FontSize="15" Margin="5,4" ToolTip="{Binding Text[ForceHelp]}" ToolTipService.ShowDuration="60000"/>
              <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">'''+button('Attack','Attack')+button('Defend','Defend')+button('Cancel','Cancel')+'''</StackPanel>
            </StackPanel>
            <TextBlock Text="{Binding Status}" Visibility="{Binding StatusVisibility}" Foreground="#FFF2D48A" FontSize="14" MaxHeight="40" TextWrapping="Wrap" TextTrimming="CharacterEllipsis" Margin="3,5,3,0" ToolTip="{Binding Status}" ToolTipService.ShowDuration="60000"/>
          </StackPanel>
        </Grid></Viewbox>
      </Border>
    </Canvas>
  </Content></Operation>
</Patch>
'''
ET.fromstring(xaml)
path.write_text(xaml,encoding='utf-8',newline='\r\n')
