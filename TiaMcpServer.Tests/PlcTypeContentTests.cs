using TiaMcpServer.OpennessWorker.Openness;
using Xunit;

namespace TiaMcpServer.Tests;

/// <summary>
/// get_type_content renders type XML through BlockSourceReconstructor. A type's
/// SW.Types.PlcStruct export matches the DB branch's HasInterface detection, so the existing
/// reconstructor must already produce a readable, timestamp-free listing — these tests pin
/// that against the real V21 export shape.
/// </summary>
public class PlcTypeContentTests
{
    private const string TypeXml =
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
              <Member Name=""regMin"" Datatype=""Int"">
                <StartValue>0</StartValue>
              </Member>
              <Member Name=""valueMax"" Datatype=""Real"" />
            </Member>
          </Section>
        </Sections>
      </Interface>
      <Name>AnalogInputSettings</Name>
      <Namespace />
    </AttributeList>
  </SW.Types.PlcStruct>
</Document>";

    [Fact]
    public void ReconstructRendersTypeXmlAsReadableListing()
    {
        string result = BlockSourceReconstructor.Reconstruct(TypeXml, programmingLanguage: null);

        Assert.Contains("AnalogInputSettings", result);
        Assert.Contains("Scaling", result);
        Assert.Contains("regMin", result);
        Assert.Contains("valueMax", result);
    }

    [Fact]
    public void ReconstructedTypeOutputIsNotRawXml()
    {
        string result = BlockSourceReconstructor.Reconstruct(TypeXml, programmingLanguage: null);

        Assert.DoesNotContain("<SW.Types.PlcStruct", result);
        Assert.DoesNotContain("<Interface>", result);
        Assert.DoesNotContain("<Member ", result);
    }

    [Fact]
    public void ReconstructedTypeOutputCarriesTypesAndStartValues()
    {
        string result = BlockSourceReconstructor.Reconstruct(TypeXml, programmingLanguage: null);

        // Member data types and start values survive reconstruction (readability contract).
        Assert.Contains("Bool", result);
        Assert.Contains("Int", result);
        Assert.Contains("Real", result);
        Assert.Contains("TRUE", result);
    }
}
