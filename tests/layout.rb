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

prefixes = %w[RogueGame BaseAI Rules BaseTownGenerator GameItems]
parts = sources.select { |path| prefixes.any? { |prefix| File.basename(path).start_with?(prefix) } }
missing = parts.reject { |path| included.include?(path.delete_prefix(source_root + '/').downcase) }
unless missing.empty?
  abort "Class part absent from csproj: #{missing.map { |p| File.basename(p) }.join(', ')}"
end

puts "Layout checks passed: #{sources.length} source files, #{test_files.length} unit test files"
