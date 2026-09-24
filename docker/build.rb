# Derive a Mono build from the original project's explicit source/resource lists.
require 'rexml/document'

project = REXML::Document.new(File.read('RogueSurvivor.csproj', encoding: 'bom|utf-8'))
files = Dir.glob('**/*', File::FNM_DOTMATCH).select { |path| File.file?(path) }
           .to_h { |path| [path.downcase, path] }
REXML::XPath.match(project, '//*').each do |node|
  tag = node.name
  name = node.attributes['Include'] || ''
  actual = files[name.tr('\\', '/').downcase]
  node.attributes['Include'] = actual.tr('/', '\\') if actual

  case tag
  when 'TargetFrameworkVersion' then node.text = 'v4.5'
  when 'DefineConstants' then node.text = 'TRACE;PORTABLE'
  when 'GenerateSerializationAssemblies' then node.text = 'Off'
  end
end
File.open('RogueSurvivor.Portable.csproj', 'w:utf-8') { |file| project.write(file) }
File.write('app.config', "<configuration><startup><supportedRuntime version=\"v4.0\"/></startup></configuration>\n")
