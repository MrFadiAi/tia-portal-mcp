using TiaMcpServer.OpennessWorker.Openness;
using Xunit;

namespace TiaMcpServer.Tests;

public class PlcTypeNamePreflightTests
{
    // Shape-true copy of a real V21 SW.Types.PlcStruct export (upstream fixture
    // AnalogInputSettings.xml): the object name is the first non-empty <Name> ELEMENT in
    // AttributeList; the Interface tree only carries Name ATTRIBUTES on Section/Member.
    private const string RealShapedTypeXml =
@"<?xml version=""1.0"" encoding=""utf-8""?>
<Document>
  <Engineering version=""V21"" />
  <SW.Types.PlcStruct ID=""0"">
    <AttributeList>
      <Interface>
        <Sections xmlns=""http://www.siemens.com/automation/Openness/SW/Interface/v5"">
          <Section Name=""None"">
            <Member Name=""Scaling"" Datatype=""Struct"">
              <Member Name=""Type"" Datatype=""Bool"">
                <StartValue>TRUE</StartValue>
              </Member>
              <Member Name=""regMin"" Datatype=""Int"" />
            </Member>
          </Section>
        </Sections>
      </Interface>
      <Name>AnalogInputSettings</Name>
      <Namespace />
    </AttributeList>
    <ObjectList>
      <MultilingualText ID=""1"" CompositionName=""Comment"">
        <ObjectList>
          <MultilingualTextItem ID=""2"" CompositionName=""Items"">
            <AttributeList>
              <Culture>en-US</Culture>
              <Text />
            </AttributeList>
          </MultilingualTextItem>
        </ObjectList>
      </MultilingualText>
    </ObjectList>
  </SW.Types.PlcStruct>
</Document>";

    [Fact]
    public void ReadsDeclaredNameFromRealShapedTypeXml()
    {
        bool ok = PlcTypeNamePreflight.TryReadDeclaredXmlName(RealShapedTypeXml, out var declaredName, out var error);

        Assert.True(ok, error);
        Assert.Equal("AnalogInputSettings", declaredName);
        Assert.Null(error);
    }

    [Fact]
    public void MemberNameAttributesNeverCountAsTheDeclaredName()
    {
        // The Interface carries Name="..." ATTRIBUTES everywhere; if the preflight key on
        // attributes it would return 'None'/'Scaling' — the ELEMENT must win.
        bool ok = PlcTypeNamePreflight.TryReadDeclaredXmlName(RealShapedTypeXml, out var declaredName, out _);

        Assert.True(ok);
        Assert.NotEqual("None", declaredName);
        Assert.NotEqual("Scaling", declaredName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsMissingContent(string? xml)
    {
        bool ok = PlcTypeNamePreflight.TryReadDeclaredXmlName(xml, out var declaredName, out var error);

        Assert.False(ok);
        Assert.Null(declaredName);
        Assert.Contains("required", error);
    }

    [Fact]
    public void RejectsMalformedXml()
    {
        bool ok = PlcTypeNamePreflight.TryReadDeclaredXmlName("<Document><SW.Types.PlcStruct>", out _, out var error);

        Assert.False(ok);
        Assert.Contains("not well-formed", error);
    }

    [Fact]
    public void RejectsXmlWithoutAnyNameElement()
    {
        string xml = "<Document><Thing><AttributeList><Interface /></AttributeList></Thing></Document>";

        bool ok = PlcTypeNamePreflight.TryReadDeclaredXmlName(xml, out var declaredName, out var error);

        Assert.False(ok);
        Assert.Null(declaredName);
        Assert.Contains("No <Name> element", error);
    }

    [Fact]
    public void SkipsEmptyNameElements()
    {
        string xml = "<Document><Name></Name><Name>RealName</Name></Document>";

        bool ok = PlcTypeNamePreflight.TryReadDeclaredXmlName(xml, out var declaredName, out var error);

        Assert.True(ok, error);
        Assert.Equal("RealName", declaredName);
    }
}
