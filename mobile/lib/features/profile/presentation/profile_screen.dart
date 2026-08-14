import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../auth/presentation/auth_controller.dart';
import '../data/photo_picker_service.dart';
import '../domain/gender.dart';
import '../domain/patient_profile.dart';
import '../domain/update_profile_request.dart';
import 'profile_controller.dart';
import 'profile_state.dart';

enum _PhotoSource { gallery, camera }

/// Client-side pre-check thresholds — deliberately mirror the backend's
/// PhotoStorageOptions default (5 MB) and only reject at the extension
/// level as a fast, obvious-mistake guard; the real validation is the
/// backend's magic-bytes decode (issue #33), which this can never replace.
const _maxPhotoSizeBytes = 5 * 1024 * 1024;
const _allowedPhotoExtensions = {'jpg', 'jpeg', 'png', 'webp', 'heic', 'heif'};

class ProfileScreen extends ConsumerStatefulWidget {
  const ProfileScreen({super.key});

  @override
  ConsumerState<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends ConsumerState<ProfileScreen> {
  final _formKey = GlobalKey<FormState>();
  final _fullNameController = TextEditingController();
  final _phoneController = TextEditingController();
  final _addressController = TextEditingController();
  final _emergencyNameController = TextEditingController();
  final _emergencyPhoneController = TextEditingController();
  DateTime? _dateOfBirth;
  Gender? _gender;
  bool _populatedFromProfile = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(profileControllerProvider.notifier).load();
    });
  }

  @override
  void dispose() {
    _fullNameController.dispose();
    _phoneController.dispose();
    _addressController.dispose();
    _emergencyNameController.dispose();
    _emergencyPhoneController.dispose();
    super.dispose();
  }

  void _populateFrom(PatientProfile profile) {
    _fullNameController.text = profile.fullName;
    _phoneController.text = profile.phone ?? '';
    _addressController.text = profile.addressLine ?? '';
    _emergencyNameController.text = profile.emergencyContactName ?? '';
    _emergencyPhoneController.text = profile.emergencyContactPhone ?? '';
    _dateOfBirth = profile.dateOfBirth;
    _gender = profile.gender;
  }

  static String? _emptyToNull(String value) => value.trim().isEmpty ? null : value.trim();

  static String _formatDate(DateTime date) => '${date.year}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';

  Future<void> _pickDateOfBirth() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: _dateOfBirth ?? DateTime(now.year - 30),
      firstDate: DateTime(1900),
      lastDate: now, // a future date of birth is impossible to pick here — mirrors the backend's own rejection
    );
    if (picked != null) {
      setState(() => _dateOfBirth = picked);
    }
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;

    final request = UpdateProfileRequest(
      fullName: _fullNameController.text.trim(),
      dateOfBirth: _dateOfBirth,
      gender: _gender,
      phone: _emptyToNull(_phoneController.text),
      addressLine: _emptyToNull(_addressController.text),
      emergencyContactName: _emptyToNull(_emergencyNameController.text),
      emergencyContactPhone: _emptyToNull(_emergencyPhoneController.text),
    );

    final success = await ref.read(profileControllerProvider.notifier).save(request);
    if (!mounted) return;

    final failure = ref.read(profileControllerProvider).saveFailure;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(success ? 'Profile saved.' : (failure?.message ?? 'Something went wrong.'))),
    );
  }

  Future<void> _showPhotoSourceSheet() async {
    final source = await showModalBottomSheet<_PhotoSource>(
      context: context,
      builder: (context) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(Icons.photo_library),
              title: const Text('Choose from gallery'),
              onTap: () => Navigator.pop(context, _PhotoSource.gallery),
            ),
            ListTile(
              leading: const Icon(Icons.photo_camera),
              title: const Text('Take a photo'),
              onTap: () => Navigator.pop(context, _PhotoSource.camera),
            ),
          ],
        ),
      ),
    );
    if (source == null || !mounted) return;

    final picker = ref.read(photoPickerServiceProvider);
    final picked = source == _PhotoSource.gallery ? await picker.pickFromGallery() : await picker.pickFromCamera();
    if (picked == null || !mounted) return;

    // Client-side pre-check (issue #34's AC) — reject an obvious mistake
    // before a network round-trip; the backend's magic-bytes decode is
    // still the real security boundary (issue #33), not this.
    if (picked.bytes.length > _maxPhotoSizeBytes) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('That photo is too large. Please choose one under 5 MB.')));
      return;
    }
    final extension = picked.fileName.contains('.') ? picked.fileName.split('.').last.toLowerCase() : '';
    if (!_allowedPhotoExtensions.contains(extension)) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Please choose a JPEG, PNG, WEBP, or HEIC photo.')));
      return;
    }

    final success = await ref.read(profileControllerProvider.notifier).uploadPhoto(picked);
    if (!mounted) return;

    final failure = ref.read(profileControllerProvider).photoUploadFailure;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(success ? 'Photo updated.' : (failure?.message ?? 'Could not upload photo.'))),
    );
  }

  Future<void> _confirmDeleteAccount() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Delete account?'),
        content: const Text('This deactivates your account and signs you out of every device. This cannot be undone from the app.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancel')),
          TextButton(
            onPressed: () => Navigator.pop(context, true),
            style: TextButton.styleFrom(foregroundColor: Theme.of(context).colorScheme.error),
            child: const Text('Delete'),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;

    final success = await ref.read(profileControllerProvider.notifier).deleteAccount();
    if (!mounted) return;

    if (success) {
      await ref.read(authControllerProvider.notifier).logout();
    } else {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not delete your account. Please try again.')));
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(profileControllerProvider);

    if (state.profile != null && !_populatedFromProfile) {
      _populatedFromProfile = true;
      _populateFrom(state.profile!);
    }

    return Scaffold(
      appBar: AppBar(title: const Text('My Profile')),
      body: _buildBody(context, state),
    );
  }

  Widget _buildBody(BuildContext context, ProfileState state) {
    if (state.isLoading && state.profile == null) {
      return const Center(child: CircularProgressIndicator());
    }

    if (state.loadFailure != null && state.profile == null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(state.loadFailure!.message, textAlign: TextAlign.center),
              const SizedBox(height: 12),
              FilledButton(onPressed: () => ref.read(profileControllerProvider.notifier).retry(), child: const Text('Retry')),
            ],
          ),
        ),
      );
    }

    final profile = state.profile!;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Form(
        key: _formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Center(child: _Avatar(state: state, onTap: _showPhotoSourceSheet)),
            const SizedBox(height: 16),
            if (profile.email != null) _ReadOnlyField(label: 'Email', value: profile.email!),
            if (profile.mobile != null) _ReadOnlyField(label: 'Mobile', value: profile.mobile!),
            const SizedBox(height: 8),
            TextFormField(
              controller: _fullNameController,
              decoration: const InputDecoration(labelText: 'Full name'),
              maxLength: 200,
              validator: (value) => (value == null || value.trim().isEmpty) ? 'Full name is required.' : null,
            ),
            ListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('Date of birth'),
              subtitle: Text(_dateOfBirth == null ? 'Not set' : _formatDate(_dateOfBirth!)),
              trailing: const Icon(Icons.calendar_today),
              onTap: _pickDateOfBirth,
            ),
            DropdownButtonFormField<Gender?>(
              initialValue: _gender,
              decoration: const InputDecoration(labelText: 'Gender'),
              items: [
                const DropdownMenuItem(child: Text('Prefer not to specify')),
                ...Gender.values.map((g) => DropdownMenuItem(value: g, child: Text(g.label))),
              ],
              onChanged: (value) => setState(() => _gender = value),
            ),
            TextFormField(
              controller: _phoneController,
              decoration: const InputDecoration(labelText: 'Phone'),
              maxLength: 30,
              keyboardType: TextInputType.phone,
            ),
            TextFormField(
              controller: _addressController,
              decoration: const InputDecoration(labelText: 'Address'),
              maxLength: 500,
              maxLines: 2,
            ),
            TextFormField(
              controller: _emergencyNameController,
              decoration: const InputDecoration(labelText: 'Emergency contact name'),
              maxLength: 200,
            ),
            TextFormField(
              controller: _emergencyPhoneController,
              decoration: const InputDecoration(labelText: 'Emergency contact phone'),
              maxLength: 30,
              keyboardType: TextInputType.phone,
            ),
            const SizedBox(height: 16),
            FilledButton(
              onPressed: state.isSaving ? null : _save,
              child: state.isSaving
                  ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Text('Save'),
            ),
            const SizedBox(height: 24),
            OutlinedButton(
              onPressed: _confirmDeleteAccount,
              style: OutlinedButton.styleFrom(foregroundColor: Theme.of(context).colorScheme.error),
              child: const Text('Delete my account'),
            ),
          ],
        ),
      ),
    );
  }
}

class _Avatar extends StatelessWidget {
  const _Avatar({required this.state, required this.onTap});

  final ProfileState state;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Stack(
        children: [
          CircleAvatar(
            radius: 48,
            backgroundImage: state.photoBytes != null ? MemoryImage(state.photoBytes!) : null,
            child: state.photoBytes == null ? const Icon(Icons.person, size: 48) : null,
          ),
          if (state.isUploadingPhoto)
            Positioned.fill(
              child: DecoratedBox(
                decoration: BoxDecoration(color: Colors.black.withValues(alpha: 0.4), shape: BoxShape.circle),
                child: const Center(child: CircularProgressIndicator()),
              ),
            ),
          Positioned(
            bottom: 0,
            right: 0,
            child: CircleAvatar(radius: 14, child: Icon(Icons.edit, size: 14, color: Theme.of(context).colorScheme.onPrimary)),
          ),
        ],
      ),
    );
  }
}

class _ReadOnlyField extends StatelessWidget {
  const _ReadOnlyField({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: TextFormField(
        initialValue: value,
        readOnly: true,
        decoration: InputDecoration(labelText: label, filled: true),
      ),
    );
  }
}
