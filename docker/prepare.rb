# Apply container compatibility changes to a private copy of the game sources.
module GamePreparation
  module_function

  def edit(root, filename)
    path = File.join(root, filename)
    original = File.binread(path)
    bom = original.start_with?("\xEF\xBB\xBF".b)
    text = (bom ? original.byteslice(3..) : original).force_encoding(Encoding::UTF_8)
    text = text.gsub("\r\n", "\n")
    updated = yield text
    return if updated == text

    updated = updated.gsub("\n", "\r\n") if original.include?("\r\n")
    File.binwrite(path, (bom ? "\xEF\xBB\xBF".b : ''.b) + updated.b)
  end

  def replace_once(text, before, after)
    return text if text.include?(after)

    unless text.scan(Regexp.new(Regexp.escape(before))).length == 1
      raise "Unsupported source version: expected one occurrence of #{before}"
    end
    text.sub(before) { after }
  end

  def exclude_backend(text, first_case)
    pattern = /^ +case #{Regexp.escape(first_case)}:.*?(?=^ +default:)/m
    matches = text.enum_for(:scan, pattern).map { Regexp.last_match }
    raise "Unsupported source version: #{first_case}" unless matches.length == 1

    match = matches.first
    return text if text[0...match.begin(0)].end_with?("#if !LINUX\n")

    text.sub(pattern) { |body| "#if !LINUX\n#{body}#endif\n" }
  end

  def configure_linux(text)
    return text if text.include?("#if LINUX\n")

    pattern = /(public static void Load\(\)\s*\{\n)(.*?)(^        \})/m
    raise 'Unsupported source version: SetupConfig.Load' unless text.scan(pattern).length == 1

    configuration = <<~CSHARP
      #if LINUX
                  Directory.CreateDirectory(DirPath);
                  Video = eVideo.VIDEO_GDI_PLUS;
                  Sound = eSound.SOUND_NOSOUND;
                  Save();
      #else
    CSHARP
    text.sub(pattern) { "#{Regexp.last_match(1)}#{configuration}#{Regexp.last_match(2)}#endif\n#{Regexp.last_match(3)}" }
  end

  def prepare_sources(root)
    edit(root, 'RogueForm.Designer.cs') { |s| exclude_backend(s, 'SetupConfig.eVideo.VIDEO_MANAGED_DIRECTX') }
    edit(root, 'Engine/RogueGame.cs') { |s| exclude_backend(s, 'SetupConfig.eSound.SOUND_MANAGED_DIRECTX') }
    edit(root, 'SetupConfig.cs') { |s| configure_linux(s) }
    edit(root, 'Gameplay/GameImages.cs') do |s|
      replace_once(s, 'string file = FOLDER + id + ".png";',
                   "string file = (FOLDER + id + \".png\").Replace('\\\\', System.IO.Path.DirectorySeparatorChar);")
    end
    edit(root, 'Program.cs') do |s|
      replace_once(s, "catch (Exception e)\n                {",
                   "catch (Exception e)\n                {\n                    Logger.WriteLine(Logger.Stage.RUN_MAIN, e.ToString());")
    end
  end
end

GamePreparation.prepare_sources('.') if $PROGRAM_NAME == __FILE__
