require 'rexml/document'

root = ENV.fetch('ROGUE_PROJECT_ROOT') { File.expand_path('..', __dir__) }
source_root = File.join(root, 'WRogue')
sources = Dir.glob(File.join(source_root, '**', '*.cs')).reject do |path|
  path.include?('/bin/') || path.include?('/obj/')
end

oversized = sources.select { |path| File.foreach(path).count > 1500 }
unless oversized.empty?
  abort "C# source exceeds 1500 lines: #{oversized.map { |p| p.delete_prefix(root + '/') }.join(', ')}"
end

test_files = Dir.glob(File.join(root, 'tests', '*.cs'))
large_tests = test_files.select { |path| File.foreach(path).count > 150 }
unless large_tests.empty?
  abort "C# test exceeds 150 lines: #{large_tests.map { |p| File.basename(p) }.join(', ')}"
end

project = REXML::Document.new(File.read(File.join(source_root, 'RogueSurvivor.csproj'), encoding: 'bom|utf-8'))
included = REXML::XPath.match(project, '//*').select { |node| node.name == 'Compile' }.map do |node|
  node.attributes['Include'].tr('\\', '/').downcase
end
abort 'Duplicate Compile entries' unless included.uniq.length == included.length

legacy = %w[
  Gameplay/AI/LOSSensor.cs Gameplay/AI/SmellSensor.cs
  UI/GDIGameCanvas.cs UI/GDIGameCanvas.Designer.cs
  Engine/MDXSoundManager.cs Engine/SFMLSoundManager.cs
  UI/DXGameCanvas.cs UI/DXGameCanvas.Designer.cs
].map(&:downcase)
missing = sources.reject do |path|
  name = path.delete_prefix(source_root + '/').downcase
  included.include?(name) || legacy.include?(name)
end
unless missing.empty?
  abort "Source absent from csproj: #{missing.map { |p| File.basename(p) }.join(', ')}"
end
stale_exceptions = legacy.reject { |name| sources.any? { |path| path.delete_prefix(source_root + '/').downcase == name } }
abort "Stale source exceptions: #{stale_exceptions.join(', ')}" unless stale_exceptions.empty?

%w[GameItems GameActors GameTiles].each do |catalog|
  body = File.read(File.join(source_root, 'Gameplay', "#{catalog}.cs"), encoding: 'bom|utf-8')[/public enum IDs\s*\{(.*?)\}/m, 1]
  abort "Missing #{catalog} IDs" unless body
  entries = body.lines.filter_map { |line| line[/^\s*([A-Z][A-Z_0-9]*)\s*(?:=\s*\d+)?\s*,?\s*$/, 1] }
  without_number = entries.reject { |entry| body.match?(/^\s*#{Regexp.escape(entry)}\s*=\s*\d+\s*,?\s*$/) }
  abort "Implicit #{catalog} IDs: #{without_number.join(', ')}" unless without_number.empty?
end

puts "Layout checks passed: #{sources.length} source files, #{test_files.length} unit test files"
