using System;
using TiaMcpServer.OpennessWorker.Openness;
using Xunit;

namespace TiaMcpServer.Tests;

/// <summary>
/// Unit tests for <see cref="BlockSourceReconstructor"/> — proves the XML→readable-STL
/// reconstruction works (the source-reconstruction feature). The class is link-compiled into
/// this assembly (it has no Siemens dependencies), so its internal API is directly testable.
/// XML shape mirrors real TIA Openness exports (verified against Main_1.xml).
/// </summary>
public class BlockSourceReconstructorTests
{
    private const string Ns = "http://www.siemens.com/automation/Openness/SW/NetworkSource/StatementList/v5";

    /// <summary>Wrap one or more &lt;StlStatement&gt; in a full single-network block export.</summary>
    private static string Block(params string[] statements)
        => BlockWithUnits(Unit(statements));

    /// <summary>Wrap several networks into one (possibly multi-document) export.</summary>
    private static string BlockWithUnits(params string[] units)
        => "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Document>"
           + "<SW.Blocks.FB ID=\"0\"><AttributeList><Name>TestBlock</Name>"
           + "<ProgrammingLanguage>STL</ProgrammingLanguage></AttributeList>"
           + "<ObjectList>" + string.Join("", units) + "</ObjectList></SW.Blocks.FB></Document>";

    private static string Unit(params string[] statements)
        => "<SW.Blocks.CompileUnit ID=\"1\"><AttributeList><NetworkSource>"
           + "<StatementList xmlns=\"" + Ns + "\">" + string.Join("", statements) + "</StatementList>"
           + "</NetworkSource></AttributeList></SW.Blocks.CompileUnit>";

    // Operand builders (match real Openness XML shape).
    private static string Global(params string[] components)
    {
        var comps = "";
        foreach (var c in components)
        {
            // First component: no HasQuotes; later ones carry HasQuotes=false (the common case).
            comps += "<Component Name=\"" + c + "\"><BooleanAttribute Name=\"HasQuotes\" Informative=\"true\">false</BooleanAttribute></Component>";
        }
        return "<Access Scope=\"GlobalVariable\"><Symbol>" + comps + "</Symbol></Access>";
    }

    private static string Local(string name)
        => "<Access Scope=\"LocalVariable\"><Symbol><Component Name=\"" + name + "\"/></Symbol></Access>";

    private static string Constant(string value)
        => "<Access Scope=\"LiteralConstant\"><Constant><ConstantValue>" + value + "</ConstantValue></Constant></Access>";

    private static string CallOperand(string blockName)
        => "<Access Scope=\"Call\"><CallInfo Name=\"" + blockName + "\" BlockType=\"FC\"/></Access>";

    private static string Stmt(string token, params string[] operands)
        => "<StlStatement><StlToken Text=\"" + token + "\"/>" + string.Join("", operands) + "</StlStatement>";

    private static string Reconstruct(string xml, string lang = "STL")
        => BlockSourceReconstructor.Reconstruct(xml, lang);

    // ------------------------------------------------------------------ guard cases

    [Fact]
    public void Null_Xml_Returns_Empty()
        => Assert.Equal(string.Empty, BlockSourceReconstructor.Reconstruct(null, "STL"));

    [Fact]
    public void Empty_Xml_Returns_Empty()
        => Assert.Equal(string.Empty, BlockSourceReconstructor.Reconstruct("", "STL"));

    [Fact]
    public void NonStl_Language_Returns_Raw_Xml_Unchanged()
    {
        var xml = Block(Stmt("A", Global("TAG")));
        var result = Reconstruct(xml, "SCL");
        Assert.Equal(xml, result); // SCL/FBD/DB pass through untouched
    }

    [Fact]
    public void Stl_Language_Match_Is_Case_Insensitive()
    {
        var xml = Block(Stmt("Assign", Global("TAG")));
        var result = Reconstruct(xml, "stl");
        Assert.Contains("=     \"TAG\"", result);
    }

    [Fact]
    public void No_CompileUnits_Returns_Raw_Xml()
    {
        // Valid XML but no networks -> nothing to reconstruct -> return raw.
        var xml = "<?xml version=\"1.0\"?><Document><Foo/></Document>";
        Assert.Equal(xml, Reconstruct(xml, "STL"));
    }

    [Fact]
    public void Malformed_Xml_Falls_Back_To_Raw()
    {
        var garbage = "this is <<not>> xml at all";
        Assert.Equal(garbage, Reconstruct(garbage, "STL"));
    }

    // ------------------------------------------------------------------ instructions

    [Fact]
    public void Assign_Token_Reconstructs_As_Equals_And_Quoted_Symbol()
    {
        var result = Reconstruct(Block(Stmt("Assign", Global("AFPAKKER_INSTALLATIE_DRAAIT"))));
        Assert.Contains("// Network 1", result);
        Assert.Contains("=     \"AFPAKKER_INSTALLATIE_DRAAIT\"", result);
    }

    [Fact]
    public void Read_Mnemonics_Are_Emitted_Verbatim()
    {
        // A / AN / O / L are already mnemonics in Openness XML -> emitted unchanged.
        var result = Reconstruct(Block(
            Stmt("A", Global("IN1")),
            Stmt("AN", Global("IN2")),
            Stmt("O", Global("IN3")),
            Stmt("L", Constant("5"))));
        Assert.Contains("A     \"IN1\"", result);
        Assert.Contains("AN     \"IN2\"", result);
        Assert.Contains("O     \"IN3\"", result);
        Assert.Contains("L     5", result);
    }

    [Theory]
    [InlineData("Assign", "=")]
    [InlineData("A_BRACK", "A(")]
    [InlineData("AN_BRACK", "AN(")]
    [InlineData("O_BRACK", "O(")]
    [InlineData("ON_BRACK", "ON(")]
    [InlineData("BRACKET", ")")]
    [InlineData("NOP_0", "NOP 0")]
    [InlineData("ADD_R", "+R")]
    [InlineData("SUB_R", "-R")]
    [InlineData("MUL_R", "*R")]
    [InlineData("DIV_R", "/R")]
    [InlineData("Rise", "FP")]
    [InlineData("Fall", "FN")]
    [InlineData("OnDelay", "SD")]
    [InlineData("OffDelay", "SF")]
    public void Special_Tokens_Map_To_Correct_Mnemonic(string token, string mnemonic)
    {
        var result = Reconstruct(Block(Stmt(token)));
        Assert.Contains(mnemonic, result);
    }

    [Fact]
    public void Bracket_Close_Is_On_Its_Own_Line()
    {
        var result = Reconstruct(Block(Stmt("BRACKET")));
        Assert.Contains("      )", result);
    }

    // ------------------------------------------------------------------ comments / blanks

    [Fact]
    public void Comment_Token_Reconstructs_As_Double_Slash_Line()
    {
        var result = Reconstruct(Block(CommentStmt("NETWORK TITLE")));
        Assert.Contains("//NETWORK TITLE", result);
    }

    /// <summary>Build a COMMENT statement carrying a LineComment/Text.</summary>
    private static string CommentStmt(string text)
        => "<StlStatement><LineComment Inserted=\"false\"><Text>" + text + "</Text></LineComment>"
           + "<StlToken Text=\"COMMENT\"/></StlStatement>";

    [Fact]
    public void Empty_Line_Token_Produces_A_Blank_Line()
    {
        var result = Reconstruct(Block(Stmt("EMPTY_LINE")));
        Assert.Contains("\n\n", result); // a blank line between the network header and EOL
    }

    // ------------------------------------------------------------------ operands

    [Fact]
    public void Dotted_Symbol_Quotes_First_And_Dots_The_Rest()
    {
        // <Component Name="DB"/><Component Name="FIELD"/> -> "DB".FIELD
        var result = Reconstruct(Block(Stmt("Assign", Global("DB_GELIJKLOOP", "AANLOOP_ACTIEF"))));
        Assert.Contains("=     \"DB_GELIJKLOOP\".AANLOOP_ACTIEF", result);
    }

    [Fact]
    public void Local_Variable_Is_Prefixed_With_Hash()
    {
        var result = Reconstruct(Block(Stmt("A", Local("temp"))));
        Assert.Contains("A     #temp", result);
    }

    [Fact]
    public void Literal_Constant_Value_Is_Rendered()
    {
        var result = Reconstruct(Block(Stmt("L", Constant("S5T#5S"))));
        Assert.Contains("L     S5T#5S", result);
    }

    [Fact]
    public void Call_Renders_Quoted_Block_Name()
    {
        var result = Reconstruct(Block(Stmt("CALL", CallOperand("FC_VERWERK_RTC"))));
        Assert.Contains("CALL     \"FC_VERWERK_RTC\"", result);
    }

    // ------------------------------------------------------------------ multi-network / structure

    [Fact]
    public void Multiple_Networks_Are_Numbered_Sequentially()
    {
        var xml = BlockWithUnits(
            Unit(Stmt("Assign", Global("A"))),
            Unit(Stmt("Assign", Global("B"))));
        var result = Reconstruct(xml);
        Assert.Contains("// Network 1", result);
        Assert.Contains("// Network 2", result);
        Assert.Contains("=     \"A\"", result);
        Assert.Contains("=     \"B\"", result);
    }

    [Fact]
    public void Network_Header_Precedes_Its_Statements()
    {
        var result = Reconstruct(Block(Stmt("Assign", Global("A"))));
        var headerIdx = result.IndexOf("// Network 1", StringComparison.Ordinal);
        var stmtIdx = result.IndexOf("=     \"A\"", StringComparison.Ordinal);
        Assert.True(headerIdx >= 0 && stmtIdx > headerIdx, "network header must come before its statements");
    }

    [Fact]
    public void Namespaced_StatementList_Is_Parsed_Via_LocalName()
    {
        // Real exports carry xmlns on StatementList -> children inherit it.
        // The reconstructor matches by LocalName, so this must still parse.
        var result = Reconstruct(Block(Stmt("Assign", Global("NAMESPACED"))));
        Assert.Contains("=     \"NAMESPACED\"", result);
    }

    [Fact]
    public void Namespaced_CompileUnit_Tag_Is_Recognised()
    {
        // Some exports use <CompileUnit>, some <SW.Blocks.CompileUnit>. Both must work.
        var xml = "<?xml version=\"1.0\"?><Document><SW.Blocks.OB ID=\"0\"><AttributeList>"
                  + "<ProgrammingLanguage>STL</ProgrammingLanguage></AttributeList><ObjectList>"
                  + "<CompileUnit ID=\"9\"><AttributeList><NetworkSource>"
                  + "<StatementList xmlns=\"" + Ns + "\">"
                  + "<StlStatement><StlToken Text=\"T\"/>" + Global("OUT") + "</StlStatement>"
                  + "</StatementList></NetworkSource></AttributeList></CompileUnit>"
                  + "</ObjectList></SW.Blocks.OB></Document>";
        var result = Reconstruct(xml);
        Assert.Contains("T     \"OUT\"", result);
    }

    [Fact]
    public void File_Separator_Lines_Are_Stripped_Before_Parsing()
    {
        var xml = "--- FILE: part.xml ---\n" + Block(Stmt("Assign", Global("SEP")));
        var result = Reconstruct(xml);
        Assert.Contains("=     \"SEP\"", result);
        Assert.DoesNotContain("--- FILE", result);
    }

    [Fact]
    public void Multiple_Concatenated_Xml_Docs_Are_All_Parsed()
    {
        var doc1 = "<?xml version=\"1.0\"?><Document>" + UnitXml("Assign", "FIRST") + "</Document>";
        var doc2 = "<?xml version=\"1.0\"?><Document>" + UnitXml("Assign", "SECOND") + "</Document>";
        var result = Reconstruct(doc1 + doc2);
        Assert.Contains("=     \"FIRST\"", result);
        Assert.Contains("=     \"SECOND\"", result);
        Assert.Contains("// Network 1", result);
        Assert.Contains("// Network 2", result);
    }

    private static string UnitXml(string token, string tag)
        => "<SW.Blocks.CompileUnit ID=\"1\"><AttributeList><NetworkSource>"
           + "<StatementList xmlns=\"" + Ns + "\">"
           + "<StlStatement><StlToken Text=\"" + token + "\"/>"
           + "<Access Scope=\"GlobalVariable\"><Symbol><Component Name=\"" + tag + "\"/></Symbol></Access>"
           + "</StlStatement></StatementList></NetworkSource></AttributeList></SW.Blocks.CompileUnit>";

    // ------------------------------------------------------------------ regression: real block

    [Fact]
    public void Regression_Real_Main_Block_Yields_Readable_Stl()
    {
        // Mirrors the AFPAKKER_INSTALLATIE_DRAAIT network from the real Main_1.xml.
        var xml = Block(
            Stmt("A", Global("SIGNAAL_VAN_PLATOFRM_START_PLUK")),
            Stmt("Assign", Global("AFPAKKER_INSTALLATIE_DRAAIT")));
        var result = Reconstruct(xml);

        Assert.Contains("A     \"SIGNAAL_VAN_PLATOFRM_START_PLUK\"", result);  // read
        Assert.Contains("=     \"AFPAKKER_INSTALLATIE_DRAAIT\"", result);      // write
        Assert.DoesNotContain("<StlToken", result);                            // raw XML must be gone
        Assert.DoesNotContain("<Component", result);
    }

    // --------------------------------------------------------------- SCL reconstruction

    private static string SclBlock(params string[] structuredTextChildren)
        => "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Document>"
           + "<SW.Blocks.FC ID=\"0\"><AttributeList><Name>SclBlock</Name>"
           + "<ProgrammingLanguage>SCL</ProgrammingLanguage></AttributeList>"
           + "<ObjectList>" + SclUnit(structuredTextChildren) + "</ObjectList></SW.Blocks.FC></Document>";

    private static string SclUnit(params string[] structuredTextChildren)
        => "<SW.Blocks.CompileUnit ID=\"1\"><AttributeList><NetworkSource>"
           + "<StructuredText xmlns=\"" + Ns + "\">" + string.Join("", structuredTextChildren)
           + "</StructuredText></NetworkSource></AttributeList></SW.Blocks.CompileUnit>";

    private static string Tok(string text) => "<Token Text=\"" + text + "\"/>";
    private static string Blk(int num) => "<Blank Num=\"" + num + "\"/>";
    private static string NL(int num = 1) => "<NewLine Num=\"" + num + "\"/>";
    private static string SclComment(string text)
        => "<LineComment><Text>" + text + "</Text></LineComment>";

    [Fact]
    public void Scl_Assignment_Reconstructs_Readably()
    {
        // "Tag" := 5;  -- Access(GlobalVar) Blank Token(:=) Blank Access(Lit 5) Token(;) NewLine
        var xml = SclBlock(
            Global("Tag"), Blk(1), Tok(":="), Blk(1), Constant("5"), Tok(";"), NL());
        var result = Reconstruct(xml, "SCL");
        Assert.Contains("\"Tag\" := 5;", result);
        Assert.DoesNotContain("<Token", result);
        Assert.DoesNotContain("<Access", result);
    }

    [Fact]
    public void Scl_Local_Variable_Prefixed_With_Hash()
    {
        var xml = SclBlock(Local("temp"), Blk(1), Tok(":="), Blk(1), Constant("1"));
        var result = Reconstruct(xml, "SCL");
        Assert.Contains("#temp := 1", result);
    }

    [Fact]
    public void Scl_Dotted_Global_Symbol()
    {
        // "DB".Member := 0
        var xml = SclBlock(Global("DB", "Member"), Blk(1), Tok(":="), Blk(1), Constant("0"));
        var result = Reconstruct(xml, "SCL");
        Assert.Contains("\"DB\".Member := 0", result);
    }

    [Fact]
    public void Scl_Line_Comment_Newline_And_Blank_Preserved()
    {
        var xml = SclBlock(
            SclComment(" header comment"), NL(),
            Tok("IF"), Blk(2), Tok("TRUE"), Tok(";"), NL(2),
            Tok("END_IF"), Tok(";"));
        var result = Reconstruct(xml, "SCL");
        Assert.Contains("// header comment", result);
        Assert.Contains("IF  TRUE;", result);   // 2-space blank between IF and TRUE
        Assert.Contains("\n\nEND_IF;", result);  // 2 newlines before END_IF
    }

    [Fact]
    public void Non_Scl_Non_Stl_Language_Passes_Through()
    {
        var xml = SclBlock(Tok("X"));
        var result = Reconstruct(xml, "FBD");
        Assert.Equal(xml, result); // FBD is not reconstructed
    }

    // ---- array-index reconstruction -------------------------------------------
    // Real TIA Openness shape (verified against Main_1.xml): the indexed
    // <Component> carries AccessModifier="Array" with a child <Access> (the index
    // value). Older TIA versions instead use Token "[" + Access + Token "]" inside
    // the Component. Without handling these, get_block_content drops the index
    // ("DB".data[1] -> "DB".data), which made the AI falsely report "all registers
    // point to the same array element".

    private static string GlobalDbIndexed(string db, string member, string indexAccessInner)
        => "<Access Scope=\"GlobalVariable\"><Symbol>"
           + "<Component Name=\"" + db + "\"/>"
           + "<Component Name=\"" + member + "\" AccessModifier=\"Array\">"
           + indexAccessInner
           + "</Component></Symbol></Access>";

    private static string IntConst(string v)
        => "<Access Scope=\"LiteralConstant\"><Constant><ConstantType>DInt</ConstantType>"
           + "<ConstantValue>" + v + "</ConstantValue></Constant></Access>";

    private static string TokenArrayIndex(string indexAccessInner)
        // Older form: Component whose children are Token "[" + Access + Token "]".
        => "<Access Scope=\"GlobalVariable\"><Symbol>"
           + "<Component Name=\"DB\"/>"
           + "<Component Name=\"data\">"
           + "<Token Text=\"[\"/>" + indexAccessInner + "<Token Text=\"]\"/>"
           + "</Component></Symbol></Access>";

    [Fact]
    public void Stl_Reconstructs_Array_Index_On_Db_Member_AccessModifierForm()
    {
        var access = GlobalDbIndexed("DB_HOLDINGREGISTER_DATA", "data", IntConst("1"));
        var src = Reconstruct(Block(Stmt("L", access)));
        Assert.Contains("\"DB_HOLDINGREGISTER_DATA\".data[1]", src);
    }

    [Fact]
    public void Stl_Reconstructs_Variable_Array_Index()
    {
        var idxLocal = "<Access Scope=\"LocalVariable\"><Symbol><Component Name=\"i\"/></Symbol></Access>";
        var access = GlobalDbIndexed("DB", "data", idxLocal);
        var src = Reconstruct(Block(Stmt("L", access)));
        Assert.Contains("\"DB\".data[#i]", src);
    }

    [Fact]
    public void Stl_Reconstructs_Array_Index_TokenBracketForm()
    {
        var access = TokenArrayIndex(IntConst("7"));
        var src = Reconstruct(Block(Stmt("L", access)));
        Assert.Contains("\"DB\".data[7]", src);
    }

    // ---- call parameters -------------------------------------------------------
    // Real TIA Openness shape (verified against Main_1.xml): <Access Scope="Call">
    // <CallInfo Name="FC_X"><Parameter Name="P"><Access>value</Access></Parameter>
    // ...</CallInfo></Access>. Each Parameter stores ONLY the value <Access> (no
    // ':=' token / newline), so the reconstructor must render "P := value" itself.
    // The C# port used to emit only the block name and drop every parameter, which
    // made the AI report "call has NO parameters" — a false finding.

    private static string CallWithParams(string blockName, params string[] parameters)
        => "<Access Scope=\"Call\"><CallInfo Name=\"" + blockName + "\" BlockType=\"FC\">"
           + string.Join("", parameters) + "</CallInfo></Access>";

    private static string Param(string name, string valueAccessInner)
        => "<Parameter Name=\"" + name + "\" Section=\"Input\">"
           + valueAccessInner + "</Parameter>";

    [Fact]
    public void Stl_Reconstructs_Call_With_Parameters()
    {
        var call = CallWithParams("SCALE_FC",
            Param("ANALOOG_INT_OF_DINT", Constant("FALSE")),
            Param("ANALOOG_INPUT_INT", Global("LENGTEMETING IO LINK KANAAL 1")),
            // multi-component (DB.Member) value
            Param("ANALOGE_METING_DATA", Global("DATA ANALOOG", "CHAMPIGNON")));
        var src = Reconstruct(Block(Stmt("Call", call)));

        Assert.Contains("\"SCALE_FC\"", src);
        Assert.Contains("ANALOOG_INT_OF_DINT := FALSE", src);
        Assert.Contains("ANALOOG_INPUT_INT := ", src);
        Assert.Contains("\"LENGTEMETING IO LINK KANAAL 1\"", src);
        Assert.Contains("ANALOGE_METING_DATA := ", src);
        Assert.Contains("\"DATA ANALOOG\".CHAMPIGNON", src);
    }

    // --------------------------------------------------------------- DB reconstruction
    // Real TIA Openness DB exports carry <SW.Blocks.GlobalDB> + <AttributeList>(Name/Number/
    // ProgrammingLanguage=DB) + <Interface><Sections xmlns="...Interface/v5"><Section Name="Static">
    // with nested <Member>s. Without DB reconstruction, Reconstruct returns the raw XML, which
    // (a) is unreadable in the Compare drill-in diff and (b) leaks <DocumentInfo><Created>
    // timestamps that differ on every export, falsely marking identical DBs as "Changed".

    private const string InterfaceNs = "http://www.siemens.com/automation/Openness/SW/Interface/v5";

    /// <summary>Build a GlobalDB export. <paramref name="sectionsInner"/> goes inside
    /// <c>&lt;Sections xmlns="...Interface/v5"&gt;</c>. <paramref name="extraHeaderChildren"/>
    /// is appended after AttributeList (used to inject a &lt;DocumentInfo&gt; timestamp).</summary>
    private static string DbBlock(string name, string number, string sectionsInner,
        string? extraHeaderChildren = null, string programmingLanguage = "DB")
        => "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Document>"
           + "<SW.Blocks.GlobalDB ID=\"0\"><AttributeList>"
           + "<Name>" + name + "</Name>"
           + "<Number>" + number + "</Number>"
           + "<ProgrammingLanguage>" + programmingLanguage + "</ProgrammingLanguage>"
           + "</AttributeList>"
           + "<Interface><Sections xmlns=\"" + InterfaceNs + "\">"
           + sectionsInner
           + "</Sections></Interface>"
           + "</SW.Blocks.GlobalDB>"
           + (extraHeaderChildren ?? "")
           + "</Document>";

    /// <summary>A single Static-section &lt;Member&gt; tree.</summary>
    private static string Section(string name, params string[] members)
        => "<Section Name=\"" + name + "\">" + string.Join("", members) + "</Section>";

    private static string LeafMember(string name, string datatype, string? startValue = null,
        string? extraAttrs = null)
    {
        var sv = startValue is null ? "" : "<StartValue>" + startValue + "</StartValue>";
        var attrs = extraAttrs is null ? "" : " " + extraAttrs;
        return "<Member Name=\"" + name + "\" Datatype=\"" + datatype + "\"" + attrs + ">"
               + "<AttributeList/>" + sv + "</Member>";
    }

    private static string StructMember(string name, string extraAttrs, params string[] children)
    {
        var attrs = string.IsNullOrEmpty(extraAttrs) ? "" : " " + extraAttrs;
        return "<Member Name=\"" + name + "\" Datatype=\"Struct\"" + attrs + ">"
               + "<AttributeList/>"
               + string.Join("", children)
               + "</Member>";
    }

    [Fact]
    public void Db_Reconstructs_As_Readable_Struct_No_Raw_Xml_Leak()
    {
        // Mirrors the user's DATA_HYDRAULIEK block ( ProgrammingLanguage=DB, GlobalDB root ).
        var xml = DbBlock("DATA_HYDRAULIEK", "13",
            Section("Static",
                StructMember("BITS", "Remanence=\"NonRetain\" Accessibility=\"Public\"",
                    LeafMember("POMP_LOOPT_TE_LANG", "Bool", "FALSE"))));

        var src = Reconstruct(xml, "DB");

        // Header identifies the block.
        Assert.Contains("DATA_BLOCK \"DATA_HYDRAULIEK\"", src);
        Assert.Contains("DB 13", src);
        // Struct parent + nested leaf with start value.
        Assert.Contains("BITS : Struct", src);
        Assert.Contains("(* Public, NonRetain *)", src);
        Assert.Contains("POMP_LOOPT_TE_LANG : Bool := FALSE;", src);
        // The two-struct delimiters.
        Assert.Contains("STRUCT", src);
        Assert.Contains("END_STRUCT", src);
        // NO raw XML leak — this is what made the Compare drill-in unreadable.
        Assert.DoesNotContain("<", src);
        Assert.DoesNotContain("</Member>", src);
        Assert.DoesNotContain("<Created>", src);
        Assert.DoesNotContain("<DocumentInfo>", src);
        Assert.DoesNotContain("<AttributeList", src);
    }

    [Fact]
    public void Db_Two_Exports_With_Different_Timestamps_Produce_Identical_Output()
    {
        // The false-"Changed" guard: same Name+Number+Interface, but different <Created>
        // timestamps inside <DocumentInfo>. Raw XML would differ; the reconstructed struct
        // text MUST be byte-identical so the compare classifier sees them as equal.
        var sectionsA = Section("Static", LeafMember("X", "Int", "5"));
        var sectionsB = Section("Static", LeafMember("X", "Int", "5"));

        var xmlA = DbBlock("DB1", "1", sectionsA,
            extraHeaderChildren: "<DocumentInfo><Created>2026-07-28T10:00:00</Created>"
                                 + "<InstalledProducts><Product Name=\"TIA Portal\"/></InstalledProducts>"
                                 + "</DocumentInfo>");
        var xmlB = DbBlock("DB1", "1", sectionsB,
            extraHeaderChildren: "<DocumentInfo><Created>2026-07-28T11:30:45</Created>"
                                 + "<InstalledProducts><Product Name=\"TIA Portal\"/></InstalledProducts>"
                                 + "</DocumentInfo>");

        var a = Reconstruct(xmlA, "DB");
        var b = Reconstruct(xmlB, "DB");
        Assert.Equal(a, b);
        // Sanity: the output really did drop the timestamps (else the assert above is vacuous).
        Assert.DoesNotContain("2026-07-28", a);
    }

    [Fact]
    public void Db_Nested_Struct_Recurses_With_Indentation()
    {
        var xml = DbBlock("NESTED", "42",
            Section("Static",
                StructMember("OUTER", "",
                    StructMember("INNER", "",
                        LeafMember("DEEP", "Int", "0")))));

        var src = Reconstruct(xml, "DB");

        // Top-level member at 2 spaces; its child at 4; the leaf at 6.
        var lines = src.Split('\n');
        Assert.Contains("  OUTER : Struct", lines);
        Assert.Contains("    INNER : Struct", lines);
        Assert.Contains("      DEEP : Int := 0;", lines);
        // END_STRUCT closes each level at the OPENER's indent.
        Assert.Contains("    END_STRUCT", lines);
        Assert.Contains("  END_STRUCT", lines);
        Assert.Contains("END_STRUCT", src); // top-level
        Assert.DoesNotContain("<", src);
    }

    [Fact]
    public void Db_Empty_Interface_Produces_Empty_Struct_No_Crash()
    {
        // No <Section>, no <Member>. Must not crash and must not leak raw XML.
        var xml = DbBlock("EMPTY", "99", "");
        var src = Reconstruct(xml, "DB");

        Assert.Contains("DATA_BLOCK \"EMPTY\"", src);
        Assert.Contains("STRUCT", src);
        Assert.Contains("END_STRUCT", src);
        Assert.DoesNotContain("<", src);
    }

    [Fact]
    public void Db_Root_GlobalDB_With_Non_Db_Language_Still_Renders_Interface()
    {
        // The DB-branch also fires when the root is <SW.Blocks.GlobalDB> even if the
        // programming-language field is missing/odd (TIA sometimes exports without it).
        var xml = DbBlock("LANGLESS", "7",
            Section("Static", LeafMember("Y", "Real", "1.5")),
            programmingLanguage: "");
        var src = Reconstruct(xml, "");

        Assert.Contains("DATA_BLOCK \"LANGLESS\"", src);
        Assert.Contains("Y : Real := 1.5;", src);
        Assert.DoesNotContain("<", src);
    }
}
