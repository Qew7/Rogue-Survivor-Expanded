#!/usr/bin/env ruby
# Update and check every published copy of the game's version.

require 'optparse'

ROOT = File.expand_path('..', __dir__)
VERSION_RE = /\A\d+\.\d+\.\d+\z/
COPIES = {
  'WRogue/SetupConfig.cs' => [
    /public const string GAME_VERSION = "(?<version>\d+\.\d+\.\d+)";/
  ],
  'WRogue/Properties/AssemblyInfo.cs' => [
    /\[assembly: AssemblyVersion\("(?<version>\d+\.\d+\.\d+)\.0"\)\]/,
    /\[assembly: AssemblyFileVersion\("(?<version>\d+\.\d+\.\d+)\.0"\)\]/
  ],
  'WRogue/mods/Deonapocalypse/authors.json' => [
    /"game_version": "(?<version>\d+\.\d+\.\d+)"/
  ],
  'README.md' => [
    /Current Expanded version: \*\*(?<version>\d+\.\d+\.\d+)\*\*/,
    /The `game_version` value must name this version \(`(?<version>\d+\.\d+\.\d+)`\)/
  ],
  'docs/save-format.md' => [
    /Saves record the Rogue Survivor Expanded version \(`(?<version>\d+\.\d+\.\d+)` at/
  ]
}.freeze

def valid_version(value)
  unless VERSION_RE.match?(value) && value.split('.').all? { |part| part.to_i <= 65_535 }
    raise ArgumentError, 'version must be three numeric components from 0 to 65535'
  end
  value
end

def updated_copy(path, patterns, version, check)
  updated = File.read(path, encoding: 'UTF-8')
  patterns.each do |pattern|
    matches = updated.to_enum(:scan, pattern).map { Regexp.last_match }
    unless matches.length == 1
      raise ArgumentError, "#{path}: expected exactly one match for #{pattern.inspect}"
    end
    match = matches.first
    if check && match[:version] != version
      raise ArgumentError, "#{path}: found #{match[:version]}, expected #{version}"
    end
    updated[match.begin(:version)...match.end(:version)] = version
  end
  updated
end

def main
  check = false
  parser = OptionParser.new do |options|
    options.banner = 'Usage: ruby tools/release_version.rb [--check | X.Y.Z]'
    options.on('--check', 'Verify copies against VERSION') { check = true }
    options.on('-h', '--help', 'Show this help') { puts options; exit }
  end
  parser.parse!
  unless (check && ARGV.empty?) || (!check && ARGV.length == 1)
    raise ArgumentError, 'use either --check or a new version'
  end

  version_file = File.join(ROOT, 'VERSION')
  version = valid_version(check ? File.read(version_file, encoding: 'UTF-8').strip : ARGV.first)
  updates = COPIES.to_h do |relative, patterns|
    path = File.join(ROOT, relative)
    [path, updated_copy(path, patterns, version, check)]
  end
  unless check
    updates.each do |path, content|
      File.write(path, content, encoding: 'UTF-8') if File.read(path, encoding: 'UTF-8') != content
    end
    File.write(version_file, "#{version}\n", encoding: 'UTF-8')
  end

  tag = ENV['GITHUB_REF_TYPE'] == 'tag' ? ENV.fetch('GITHUB_REF_NAME', '') : ''
  if check && tag.start_with?('v') && tag != "v#{version}"
    raise ArgumentError, "release tag #{tag} does not match VERSION #{version}"
  end
  puts "Version #{version}: #{check ? 'all copies match' : 'release files updated'}"
end

begin
  main
rescue ArgumentError, OptionParser::ParseError, SystemCallError => error
  warn "release_version: #{error.message}"
  exit 1
end
