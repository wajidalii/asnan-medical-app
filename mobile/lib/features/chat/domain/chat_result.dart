import 'chat_failure.dart';

sealed class ChatResult<T> {
  const ChatResult();
}

class ChatSuccess<T> extends ChatResult<T> {
  const ChatSuccess(this.value);

  final T value;
}

class ChatError<T> extends ChatResult<T> {
  const ChatError(this.failure);

  final ChatFailure failure;
}
